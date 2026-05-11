using System.Diagnostics;
using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// PowerPoint 파일(PPT/PPTX/ODP)을 PDF 또는 페이지별 이미지로 변환합니다.
/// LibreOffice headless 또는 PowerPoint COM을 사용합니다.
/// </summary>
public sealed class PptProvider : IConverterProvider
{
    private static readonly string[] PptInputs = { ".pptx", ".ppt", ".odp" };

    private static readonly string[] PptOutputs =
        { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".tif", ".tiff" };

    private readonly PdfProvider _pdfProvider;

    public PptProvider(PdfProvider pdfProvider)
    {
        _pdfProvider = pdfProvider;
    }

    public ProviderCapability Capability { get; } = new(
        Id: "ppt",
        DisplayName: "프레젠테이션 (PPT / PPTX)",
        SupportedConversions: ProviderCapability.PairsFromMatrix(PptInputs, PptOutputs),
        Status: ProviderStatus.RequiresExternal,
        Summary: "PowerPoint(PPT/PPTX) 또는 ODP를 PDF로 변환하거나 슬라이드별 이미지로 렌더링합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "Microsoft PowerPoint 또는 LibreOffice",
                Description: "PPT → PDF 변환에 둘 중 하나가 필요합니다. 둘 다 없으면 LibreOffice 설치를 권장합니다.",
                DownloadUrl: "https://www.libreoffice.org/download/",
                IsRequired: true),
        },
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (IsPowerPointComAvailable())
            return Task.FromResult(ProviderAvailability.Ready);
        if (ExternalToolDetector.TryFindLibreOfficeSoffice(out _))
            return Task.FromResult(ProviderAvailability.Ready);

        return Task.FromResult(ProviderAvailability.NotReady(
            "Microsoft PowerPoint 또는 LibreOffice가 설치되어 있어야 합니다.",
            Capability.ExternalDependencies));
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var outExt = ConversionPair.Normalize(outputExtension);
        var tempPdf = Path.Combine(Path.GetTempPath(),
            $"e2e_{Guid.NewGuid():N}_{Path.GetFileNameWithoutExtension(sourcePath)}.pdf");

        try
        {
            progress?.Report(0.05);

            var converted = false;
            string? failureReason = null;

            // LibreOffice 우선 시도
            if (ExternalToolDetector.TryFindLibreOfficeSoffice(out var soffice))
            {
                converted = await ConvertWithLibreOfficeAsync(soffice, sourcePath, tempPdf, cancellationToken)
                    .ConfigureAwait(false);
                if (!converted) failureReason = "LibreOffice 변환에 실패했습니다.";
            }

            // LibreOffice 실패 시 PowerPoint COM 시도
            if (!converted && IsPowerPointComAvailable())
            {
                try
                {
                    converted = ConvertWithPowerPointCom(sourcePath, tempPdf);
                    if (!converted) failureReason = "Microsoft PowerPoint 변환에 실패했습니다.";
                }
                catch (Exception ex)
                {
                    failureReason = $"Microsoft PowerPoint 변환 오류: {ex.Message}";
                }
            }

            if (!converted)
                return ConvertResult.Fail(sourcePath, failureReason ?? "PPT → PDF 외부 변환 도구가 필요합니다.");

            progress?.Report(0.55);

            // PDF 출력이면 바로 복사
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

            // 이미지 출력이면 PdfProvider를 통해 렌더링
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

    private static bool ConvertWithPowerPointCom(string sourcePath, string targetPdf)
    {
        const int ppFixedFormatTypePDF = 2; // PpFixedFormatType.ppFixedFormatTypePDF
        var pptType = Type.GetTypeFromProgID("PowerPoint.Application");
        if (pptType is null) return false;

        dynamic? ppt = Activator.CreateInstance(pptType);
        if (ppt is null) return false;
        try
        {
            ppt.Visible = 1; // PowerPoint COM은 Visible=true여야 안정적
            ppt.DisplayAlerts = 0;

            dynamic presentation = ppt.Presentations.Open(
                Path.GetFullPath(sourcePath),
                ReadOnly: -1,   // msoTrue
                Untitled: 0,    // msoFalse
                WithWindow: 0); // msoFalse
            try
            {
                presentation.ExportAsFixedFormat(
                    targetPdf,
                    ppFixedFormatTypePDF,
                    Intent: 2,          // ppFixedFormatIntentPrint
                    FrameSlides: 0,     // msoFalse
                    HandoutOrder: 1,    // ppPrintHandoutVerticalFirst
                    OutputType: 1,      // ppPrintOutputSlides
                    PrintHiddenSlides: 0);
            }
            finally
            {
                presentation.Close();
            }
            return File.Exists(targetPdf);
        }
        finally
        {
            try { ppt.Quit(); } catch { }
        }
    }

    internal static bool IsPowerPointComAvailable()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("PowerPoint.Application");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
