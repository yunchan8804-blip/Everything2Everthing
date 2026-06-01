using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// AI 텍스트 변환 Provider. 요약·번역·교정을 LLM(OpenAI/Anthropic)으로 수행한다.
/// 설계 불변식: 키가 없으면 CheckAvailabilityAsync가 NotReady를 반환해 비활성되고, 기존 변환은 영향 없다.
/// 일반 변환과 충돌하지 않도록 txt→txt / md→md self-edge(동일 포맷, 내용만 가공)만 노출한다.
/// </summary>
public sealed class LlmProvider : IConverterProvider
{
    private readonly ISettingsStore _settings;

    public LlmProvider(ISettingsStore settings) => _settings = settings;

    public ProviderCapability Capability { get; } = new(
        Id: "ai",
        DisplayName: "AI 텍스트 (요약·번역·교정)",
        SupportedConversions: new[]
        {
            new ConversionPair(".txt", ".txt", LossClass.Recode),
            new ConversionPair(".md", ".md", LossClass.Recode),
        },
        Status: ProviderStatus.RequiresExternal,
        Summary: "OpenAI/Anthropic API 또는 Codex CLI(ChatGPT 구독 OAuth, 키 불필요)로 텍스트를 요약·번역·교정합니다 (✨AI · 네트워크 필요). 키도 Codex도 없으면 비활성됩니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "OpenAI 또는 Anthropic API 키",
                Description: "설정에서 API 키를 입력하면 활성화됩니다. 환경변수 OPENAI_API_KEY / ANTHROPIC_API_KEY 도 인식합니다.",
                DownloadUrl: "https://platform.openai.com/api-keys",
                IsRequired: true),
        },
        RoadmapNote: "이미지 캡션(비전)·메타데이터 추출(JSON)·Codex CLI OAuth는 후속 확장.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var (client, _) = ResolveClient(new AiOptions());
        if (client is null)
            return Task.FromResult(ProviderAvailability.NotReady(
                "AI API 키가 설정되지 않았습니다. 설정에서 OpenAI 또는 Anthropic 키를 입력하세요 (또는 환경변수 OPENAI_API_KEY/ANTHROPIC_API_KEY).",
                Capability.ExternalDependencies));
        return Task.FromResult(ProviderAvailability.Ready);
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var (client, model) = ResolveClient(options.Ai);
        if (client is null)
            return ConvertResult.Fail(sourcePath, "AI API 키가 없습니다. 설정에서 키를 입력하세요.");

        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var suffix = "_" + (options.Ai.Task ?? "ai");
        var outPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, suffix, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(outPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        try
        {
            var input = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(input))
                return ConvertResult.Fail(sourcePath, "입력 텍스트가 비어 있습니다.");

            var (system, user) = BuildPrompt(options.Ai, input);
            progress?.Report(0.2);

            var result = await client.CompleteAsync(system, user, model, options.Ai.MaxOutputTokens, cancellationToken)
                .ConfigureAwait(false);
            progress?.Report(0.9);

            var tmp = outPath + ".tmp";
            await File.WriteAllTextAsync(tmp, result, new System.Text.UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmp, outPath);

            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { outPath });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return ConvertResult.Fail(sourcePath, $"AI 변환 실패: {ex.Message}", ex);
        }
    }

    private (IChatClient? client, string model) ResolveClient(AiOptions ai)
    {
        var backend = (ai.Backend ?? "auto").ToLowerInvariant();
        var openaiKey = GetKey("openai");
        var anthropicKey = GetKey("anthropic");

        if (backend == "openai")
            return openaiKey is null ? (null, "") : (new OpenAiChatClient(openaiKey), ai.Model ?? "gpt-4o-mini");
        if (backend == "anthropic")
            return anthropicKey is null ? (null, "") : (new AnthropicChatClient(anthropicKey), ai.Model ?? "claude-3-5-sonnet-latest");
        if (backend == "codex")
            return ExternalToolDetector.IsCodexAvailable() ? (new CodexChatClient(), ai.Model ?? "") : (null, "");

        // auto: API 키 우선, 없으면 Codex CLI(ChatGPT 구독 OAuth)
        if (openaiKey is not null) return (new OpenAiChatClient(openaiKey), ai.Model ?? "gpt-4o-mini");
        if (anthropicKey is not null) return (new AnthropicChatClient(anthropicKey), ai.Model ?? "claude-3-5-sonnet-latest");
        if (ExternalToolDetector.IsCodexAvailable()) return (new CodexChatClient(), ai.Model ?? "");
        return (null, "");
    }

    private string? GetKey(string provider)
    {
        var stored = _settings.Get($"{provider}.apikey");
        if (!string.IsNullOrWhiteSpace(stored)) return stored;
        var env = Environment.GetEnvironmentVariable(provider == "openai" ? "OPENAI_API_KEY" : "ANTHROPIC_API_KEY");
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }

    internal static (string system, string user) BuildPrompt(AiOptions ai, string input)
    {
        var task = (ai.Task ?? "summarize").ToLowerInvariant();
        var system = task switch
        {
            "summarize" => "너는 문서 요약 도우미다. 입력의 핵심을 간결하고 명확하게 요약하라. 요약문만 출력하라.",
            "translate" => $"너는 전문 번역가다. 입력을 {ai.TargetLanguage ?? "영어"}(으)로 자연스럽게 번역하라. 번역문만 출력하라.",
            "proofread" => "너는 교정 도우미다. 입력의 오탈자·문법·어색한 표현·줄바꿈을 정리하되 원래 의미는 보존하라. 교정된 본문만 출력하라.",
            "custom" => string.IsNullOrWhiteSpace(ai.Instruction) ? "입력 텍스트를 처리하라." : ai.Instruction!,
            _ => "입력 텍스트를 처리하라.",
        };
        return (system, input);
    }
}
