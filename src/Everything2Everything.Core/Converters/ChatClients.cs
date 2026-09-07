using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Everything2Everything.Core.Converters;

/// <summary>LLM 채팅 완성 클라이언트 추상화. OpenAI/Anthropic을 동일 인터페이스로 다룬다.</summary>
public interface IChatClient
{
    string Name { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, string model, int maxTokens, CancellationToken ct);
}

public sealed class OpenAiChatClient : IChatClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private readonly string _apiKey;

    public OpenAiChatClient(string apiKey) => _apiKey = apiKey;
    public string Name => "OpenAI";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, string model, int maxTokens, CancellationToken ct)
    {
        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            max_tokens = maxTokens,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI API 오류 ({(int)resp.StatusCode}): {Truncate(json)}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static string Truncate(string s) => s.Length > 400 ? s[..400] : s;
}

public sealed class AnthropicChatClient : IChatClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private readonly string _apiKey;

    public AnthropicChatClient(string apiKey) => _apiKey = apiKey;
    public string Name => "Anthropic";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, string model, int maxTokens, CancellationToken ct)
    {
        var body = new
        {
            model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new object[]
            {
                new { role = "user", content = userPrompt },
            },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic API 오류 ({(int)resp.StatusCode}): {Truncate(json)}");

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content");
        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
            if (block.TryGetProperty("text", out var text))
                sb.Append(text.GetString());
        return sb.ToString();
    }

    private static string Truncate(string s) => s.Length > 400 ? s[..400] : s;
}

/// <summary>
/// OpenAI Codex CLI를 non-interactive로 호출하는 백엔드. ChatGPT 구독 OAuth(auth.json)를 그대로
/// 재사용하므로 API 키 없이 동작한다. `codex exec --skip-git-repo-check --ephemeral -o &lt;file&gt; -`
/// 형태로 프롬프트를 stdin으로 전달하고, 최종 메시지를 파일에서 읽는다.
/// </summary>
public sealed class CodexChatClient : IChatClient
{
    public string Name => "Codex CLI (OAuth)";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, string model, int maxTokens, CancellationToken ct)
    {
        var prompt = string.IsNullOrWhiteSpace(systemPrompt) ? userPrompt : systemPrompt + "\n\n---\n\n" + userPrompt;
        var outFile = Path.Combine(Path.GetTempPath(), "e2e_codex_" + Guid.NewGuid().ToString("N") + ".txt");

        var args = new List<string> { "/c", "codex", "exec", "--skip-git-repo-check", "--ephemeral", "-o", outFile };
        if (!string.IsNullOrWhiteSpace(model)) { args.Add("-m"); args.Add(model); }
        args.Add("-"); // 프롬프트를 stdin으로 (인자 이스케이프 회피)

        try
        {
            var r = await ExternalProcessRunner.RunAsync(
                "cmd.exe", args, TimeSpan.FromMinutes(5), workingDirectory: null,
                cancellationToken: ct, stdinText: prompt).ConfigureAwait(false);

            if (r.TimedOut)
                throw new InvalidOperationException("Codex CLI 응답이 시간 초과되었습니다 (5분).");

            if (File.Exists(outFile))
            {
                var msg = await File.ReadAllTextAsync(outFile, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(msg)) return msg.Trim();
            }

            if (!r.Success)
            {
                var detail = !string.IsNullOrWhiteSpace(r.StdErr) ? r.StdErr : r.StdOut;
                throw new InvalidOperationException($"Codex CLI 오류 (exit {r.ExitCode}): {Truncate(detail)}");
            }
            return r.StdOut.Trim();
        }
        finally
        {
            try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
        }
    }

    private static string Truncate(string s) => s.Length > 400 ? s[..400] : s;
}

public sealed class AgyChatClient : IChatClient
{
    private readonly string _agyPath;

    public AgyChatClient(string? agyPath = null)
    {
        if (!string.IsNullOrWhiteSpace(agyPath))
        {
            _agyPath = agyPath;
        }
        else if (ExternalToolDetector.IsAgyAvailable(out var detected))
        {
            _agyPath = detected;
        }
        else
        {
            _agyPath = "agy";
        }
    }

    public string Name => "Antigravity CLI (agy)";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, string model, int maxTokens, CancellationToken ct)
    {
        var prompt = string.IsNullOrWhiteSpace(systemPrompt) ? userPrompt : systemPrompt + "\n\n---\n\n" + userPrompt;

        var args = new List<string>
        {
            "-p", prompt,
            "--dangerously-skip-permissions",
            "--disable-slash-commands",
            "--output-format", "text"
        };
        if (!string.IsNullOrWhiteSpace(model))
        {
            args.Add("--model");
            args.Add(model);
        }

        var r = await ExternalProcessRunner.RunAsync(
            _agyPath, args, TimeSpan.FromMinutes(5), workingDirectory: null,
            cancellationToken: ct).ConfigureAwait(false);

        if (r.TimedOut)
            throw new InvalidOperationException("Antigravity CLI 응답이 시간 초과되었습니다 (5분).");

        if (!r.Success)
        {
            var detail = !string.IsNullOrWhiteSpace(r.StdErr) ? r.StdErr : r.StdOut;
            throw new InvalidOperationException($"Antigravity CLI 오류 (exit {r.ExitCode}): {Truncate(detail)}");
        }

        return r.StdOut.Trim();
    }

    private static string Truncate(string s) => s.Length > 400 ? s[..400] : s;
}

/// <summary>
/// 로컬 또는 네트워크에 기동된 Switchboard Gateway (http://127.0.0.1:8787)를 호출하는 백엔드.
/// 게이트웨이의 /chat 엔드포인트와 통신하며, 게이트웨이가 관리하는 에이전트(agy, codex 등)를 통해 텍스트를 완성한다.
/// </summary>
public sealed class SwitchboardChatClient : IChatClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly string _endpoint;

    public SwitchboardChatClient(string? endpoint = null)
    {
        var raw = string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:8787" : endpoint.Trim();
        _endpoint = raw.TrimEnd('/');
    }

    public string Name => "Switchboard Gateway";
    public string Endpoint => _endpoint;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, string model, int maxTokens, CancellationToken ct)
    {
        var message = string.IsNullOrWhiteSpace(systemPrompt) ? userPrompt : systemPrompt + "\n\n---\n\n" + userPrompt;
        var payload = new
        {
            message,
            sessionId = "e2e-" + Guid.NewGuid().ToString("N")[..8],
            model = string.IsNullOrWhiteSpace(model) ? null : model,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/chat");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Switchboard 게이트웨이 오류 ({(int)resp.StatusCode}): {Truncate(json)}");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("reply", out var replyProp))
            return replyProp.GetString() ?? "";

        throw new InvalidOperationException("Switchboard 게이트웨이 응답에 'reply' 필드가 없습니다.");
    }

    private static string Truncate(string s) => s.Length > 400 ? s[..400] : s;
}

