using Everything2Everything.Core.Providers;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// PDF 재압축/최적화 Provider. pdf→pdf 동일포맷 변환을 그래프 엣지로 노출해
/// ConversionEngine의 동일포맷 Skip을 우회한다. P1은 PDFsharp(MIT) in-process 구조 최적화(Light).
/// Strong(렌더 재인코딩)·Max(Ghostscript 외부)는 후속 단계에서 확장.
/// </summary>
public sealed class PdfToolProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "pdf-tool",
        DisplayName: "PDF 압축",
        SupportedConversions: new[] { new ConversionPair(".pdf", ".pdf", LossClass.Container) },
        Status: ProviderStatus.Available,
        Summary: "PDF를 재압축해 용량을 줄입니다 (구조 최적화). 전자서명·XFA 폼·PDF/A 준수는 재저장 과정에서 유실될 수 있습니다.",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: "Strong(렌더 재인코딩)·Max(Ghostscript) 압축 레벨은 후속 추가 예정");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
        => Task.Run(() => Compress(sourcePath, outputDirectory, options, progress, cancellationToken), cancellationToken);

    private static ConvertResult Compress(
        string sourcePath,
        string outputDirectory,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, "_compressed", ".pdf", options.OnCollision);
        if (OutputPathHelper.ShouldSkip(outPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        progress?.Report(0.1);

        // 임시 파일에 먼저 저장한 뒤 원자적으로 교체 — Save 도중 예외가 나도 손상된 출력이 잔존하지 않게.
        var tmp = outPath + ".tmp";
        try
        {
            using (var doc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify))
            {
                doc.Options.CompressContentStreams = true;
                doc.Options.NoCompression = false;
                doc.Options.EnableCcittCompressionForBilevelImages = true;
                cancellationToken.ThrowIfCancellationRequested();
                doc.Save(tmp);
            }

            progress?.Report(0.9);

            // 재압축이 오히려 더 커지면 원본을 사용(출력이 입력보다 크지 않도록 보장).
            var before = new FileInfo(sourcePath).Length;
            var after = new FileInfo(tmp).Length;
            if (after > before)
            {
                File.Copy(sourcePath, outPath, overwrite: true);
                File.Delete(tmp);
            }
            else
            {
                if (File.Exists(outPath)) File.Delete(outPath);
                File.Move(tmp, outPath);
            }
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 임시 정리 실패 무시 */ }
            throw;
        }

        progress?.Report(1.0);
        return ConvertResult.Ok(sourcePath, new[] { outPath });
    }
}
