using EverythingToJpeg.Core.Providers;

namespace EverythingToJpeg.Core.Converters;

public sealed class HwpxProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "hwpx",
        DisplayName: "한글 문서 (HWP / HWPX)",
        Extensions: new[] { ".hwp", ".hwpx" },
        Status: ProviderStatus.ComingSoon,
        Summary: "한글(HWP/HWPX) 문서를 PDF로 변환한 뒤 페이지별 JPEG로 저장합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "LibreOffice + H2Orestart 확장",
                Description: "한글 파일을 LibreOffice가 읽도록 해 주는 오픈소스 확장입니다.",
                DownloadUrl: "https://github.com/ebandal/H2Orestart",
                IsRequired: true),
        },
        RoadmapNote: "Phase 2 — H2Orestart + soffice headless 파이프라인. 한컴오피스 SDK 연동도 검토.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.NotReady("아직 구현되지 않았습니다. 곧 지원 예정입니다."));

    public Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, ConvertOptions options,
        IProgress<double>? progress, CancellationToken cancellationToken)
        => Task.FromResult(ConvertResult.Skip(sourcePath, "HWP/HWPX 변환은 곧 지원 예정입니다."));
}
