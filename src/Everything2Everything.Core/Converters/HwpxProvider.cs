using System.Diagnostics;
using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Converters;

public sealed class HwpxProvider : IConverterProvider
{
    private static readonly string[] HwpInputs = { ".hwp", ".hwpx" };

    private static readonly string[] HwpImageOutputs =
        { ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".tif", ".tiff" };

    private readonly PdfProvider _pdfProvider;

    public HwpxProvider() : this(new PdfProvider()) { }

    public HwpxProvider(PdfProvider pdfProvider)
    {
        _pdfProvider = pdfProvider;
    }

    public ProviderCapability Capability { get; } = new(
        Id: "hwpx",
        DisplayName: "한글 문서 (HWP / HWPX)",
        SupportedConversions: ProviderCapability.PairsFromMatrix(HwpInputs, new[] { ".pdf" }, LossClass.Recode)
            .Concat(ProviderCapability.PairsFromMatrix(HwpInputs, HwpImageOutputs, LossClass.Rasterize)).ToList(),
        Status: ProviderStatus.RequiresExternal,
        Summary: "한글(HWP/HWPX) 문서를 LibreOffice + H2Orestart로 PDF 변환 후 PDF 또는 페이지별 이미지로 저장합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "LibreOffice",
                Description: "한글 변환에 필요한 헤드리스 오피스 엔진.",
                DownloadUrl: "https://www.libreoffice.org/download/",
                IsRequired: true),
            new ExternalDependency(
                Name: "H2Orestart 확장",
                Description: "LibreOffice가 한글 파일을 읽도록 하는 오픈소스 확장. 다운로드한 oxt 파일을 LibreOffice에서 더블클릭해 설치.",
                DownloadUrl: "https://github.com/ebandal/H2Orestart/releases",
                IsRequired: true),
        },
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (!ExternalToolDetector.TryFindLibreOfficeSoffice(out _))
            return Task.FromResult(ProviderAvailability.NotReady(
                "LibreOffice가 설치되어 있지 않습니다.",
                Capability.ExternalDependencies));

        if (!ExternalToolDetector.IsH2OrestartInstalled())
            return Task.FromResult(ProviderAvailability.NotReady(
                "H2Orestart 확장이 설치되어 있지 않습니다. https://github.com/ebandal/H2Orestart/releases 에서 .oxt 다운로드 후 LibreOffice에서 설치하세요.",
                Capability.ExternalDependencies));

        return Task.FromResult(ProviderAvailability.Ready);
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!ExternalToolDetector.TryFindLibreOfficeSoffice(out var soffice))
            return ConvertResult.Fail(sourcePath, "LibreOffice가 필요합니다.");

        var outExt = ConversionPair.Normalize(outputExtension);
        var tempPdf = Path.Combine(Path.GetTempPath(),
            $"e2e_{Guid.NewGuid():N}_{Path.GetFileNameWithoutExtension(sourcePath)}.pdf");

        try
        {
            progress?.Report(0.05);

            var converted = await ConvertWithLibreOfficeAsync(soffice, sourcePath, tempPdf, cancellationToken)
                .ConfigureAwait(false);

            if (!converted)
                return ConvertResult.Fail(sourcePath,
                    "LibreOffice 변환에 실패했습니다. H2Orestart 확장이 정상 설치되어 있는지 확인하세요.");

            progress?.Report(0.55);

            if (outExt == ".pdf")
            {
                var baseName = Path.GetFileNameWithoutExtension(sourcePath);
                var finalPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, ".pdf", options.OnCollision);
                if (OutputPathHelper.ShouldSkip(finalPath, options.OnCollision))
                    return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");
                File.Copy(tempPdf, finalPath, overwrite: options.OnCollision == NameCollision.Overwrite);
                progress?.Report(1.0);
                return ConvertResult.Ok(sourcePath, new[] { finalPath });
            }

            var inner = new Progress<double>(p => progress?.Report(0.55 + p * 0.45));
            return _pdfProvider.ConvertCore(tempPdf, outputDirectory, outExt, options, inner, cancellationToken)
                with { SourcePath = sourcePath };
        }
        finally
        {
            try { if (File.Exists(tempPdf)) File.Delete(tempPdf); } catch { }
        }
    }

    private static async Task<bool> ConvertWithLibreOfficeAsync(string sofficePath, string sourcePath, string targetPdf, CancellationToken ct)
    {
        var outDir = Path.GetDirectoryName(targetPdf)!;
        var psi = new ProcessStartInfo
        {
            FileName = sofficePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add("--nofirststartwizard");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(sourcePath);

        using var proc = Process.Start(psi);
        if (proc is null) return false;

        try
        {
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            throw;
        }

        if (proc.ExitCode != 0) return false;

        var produced = Path.Combine(outDir, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
        if (!File.Exists(produced)) return false;

        if (!string.Equals(produced, targetPdf, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(targetPdf)) File.Delete(targetPdf);
            File.Move(produced, targetPdf);
        }
        return File.Exists(targetPdf);
    }
}
