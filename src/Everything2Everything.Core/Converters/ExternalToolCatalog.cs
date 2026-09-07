namespace Everything2Everything.Core.Converters;

/// <summary>설치 방식 분류. Winget = 자동 설치 가능, Manual = 사용자 안내만(라이선스/URL 제약).</summary>
public enum ExternalToolInstallKind
{
    Winget,
    Manual,
}

/// <summary>첫 실행 설치 마법사가 다루는 외부 도구 한 항목의 정의. 감지 함수는 설치 여부 판정을 담당한다.</summary>
public sealed class ExternalToolDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required ExternalToolInstallKind Kind { get; init; }
    public string? WingetId { get; init; }
    public string? ManualNote { get; init; }
    public required Func<bool> IsInstalled { get; init; }
}

/// <summary>
/// 앱이 의존하는 외부 도구의 정적 카탈로그. winget 패키지 ID는 기기에서 실검증된 값이다.
/// (Gyan.FFmpeg / TheDocumentFoundation.LibreOffice / JohnMacFarlane.Pandoc /
///  ImageMagick.ImageMagick / Google.AntigravityCLI / OpenAI.Codex)
/// H2Orestart는 GPL이고 프로젝트 원칙(번들 금지·외부 조달)상 자동 설치하지 않고 안내만 한다.
/// </summary>
public static class ExternalToolCatalog
{
    public static IReadOnlyList<ExternalToolDefinition> All { get; } = new[]
    {
        new ExternalToolDefinition
        {
            Key = "ffmpeg",
            DisplayName = "FFmpeg",
            Description = "영상/오디오 변환 (mp4·webm·mp3·flac…). ffmpeg·ffprobe 포함.",
            Kind = ExternalToolInstallKind.Winget,
            WingetId = "Gyan.FFmpeg",
            IsInstalled = () => ExternalToolDetector.TryFindFfmpeg(out _),
        },
        new ExternalToolDefinition
        {
            Key = "libreoffice",
            DisplayName = "LibreOffice",
            Description = "한글/Word/문서 변환 (DOCX→PDF·이미지).",
            Kind = ExternalToolInstallKind.Winget,
            WingetId = "TheDocumentFoundation.LibreOffice",
            IsInstalled = () => ExternalToolDetector.TryFindLibreOfficeSoffice(out _),
        },
        new ExternalToolDefinition
        {
            Key = "pandoc",
            DisplayName = "Pandoc",
            Description = "마크업 변환 (md·rst·latex·epub…).",
            Kind = ExternalToolInstallKind.Winget,
            WingetId = "JohnMacFarlane.Pandoc",
            IsInstalled = () => ExternalToolDetector.TryFindPandoc(out _),
        },
        new ExternalToolDefinition
        {
            Key = "imagemagick",
            DisplayName = "ImageMagick",
            Description = "HEIC 등 이미지 처리 변환.",
            Kind = ExternalToolInstallKind.Winget,
            WingetId = "ImageMagick.ImageMagick",
            IsInstalled = () => ExternalToolDetector.TryFindMagick(out _),
        },
        new ExternalToolDefinition
        {
            Key = "h2orestart",
            DisplayName = "H2Orestart (한글 확장)",
            Description = "LibreOffice에서 .hwp/.hwpx 열기용 UNO 확장.",
            Kind = ExternalToolInstallKind.Manual,
            ManualNote = "LibreOffice 설치 후 확장 관리자(Tools → Extension Manager)에서 H2Orestart.oxt를 직접 추가하세요.",
            IsInstalled = () => ExternalToolDetector.IsH2OrestartInstalled(),
        },
        new ExternalToolDefinition
        {
            Key = "agy",
            DisplayName = "Antigravity CLI (agy)",
            Description = "AI 변환 백엔드 (Google OAuth · API 키 불필요).",
            Kind = ExternalToolInstallKind.Winget,
            WingetId = "Google.AntigravityCLI",
            IsInstalled = () => ExternalToolDetector.IsAgyAvailable(out _),
        },
        new ExternalToolDefinition
        {
            Key = "codex",
            DisplayName = "Codex CLI",
            Description = "AI 변환 백엔드 (ChatGPT 구독 OAuth).",
            Kind = ExternalToolInstallKind.Winget,
            WingetId = "OpenAI.Codex",
            IsInstalled = () => ExternalToolDetector.IsCodexAvailable(),
        },
    };

    public static ExternalToolDefinition? Find(string key)
        => All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}