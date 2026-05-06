using EverythingToJpeg.Core.Providers;

namespace EverythingToJpeg.Core.Converters;

public sealed class HtmlProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "html",
        DisplayName: "HTML / 웹 페이지",
        Extensions: new[] { ".html", ".htm" },
        Status: ProviderStatus.ComingSoon,
        Summary: "HTML/HTM 파일을 WebView2로 헤드리스 렌더링하여 JPEG로 캡처합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "Microsoft Edge WebView2 Runtime",
                Description: "Windows 11에는 기본 포함되어 있습니다.",
                DownloadUrl: "https://developer.microsoft.com/microsoft-edge/webview2/",
                IsRequired: true),
        },
        RoadmapNote: "Phase 2 — WebView2 헤드리스 캡처 + 사용자 정의 viewport.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.NotReady("아직 구현되지 않았습니다. 곧 지원 예정입니다."));

    public Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, ConvertOptions options,
        IProgress<double>? progress, CancellationToken cancellationToken)
        => Task.FromResult(ConvertResult.Skip(sourcePath, "HTML 변환은 곧 지원 예정입니다."));
}
