using System.Diagnostics;
using EverythingToJpeg.Core.Providers;

namespace EverythingToJpeg.Core.Converters;

public sealed class DocxProvider : IConverterProvider
{
    private readonly PdfProvider _pdfProvider;

    public DocxProvider(PdfProvider pdfProvider)
    {
        _pdfProvider = pdfProvider;
    }

    public ProviderCapability Capability { get; } = new(
        Id: "docx",
        DisplayName: "Word 문서 (DOCX)",
        Extensions: new[] { ".docx", ".doc" },
        Status: ProviderStatus.RequiresExternal,
        Summary: "DOCX/DOC 문서를 PDF로 변환한 뒤 페이지별 JPEG로 저장합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "Microsoft Word 또는 LibreOffice",
                Description: "DOCX → PDF 변환에 둘 중 하나가 필요합니다. 둘 다 없으면 LibreOffice 설치를 권장합니다.",
                DownloadUrl: "https://www.libreoffice.org/download/",
                IsRequired: true),
        },
        RoadmapNote: "향후 OpenXML 기반 자체 렌더링 검토.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (ExternalToolDetector.IsWordComAvailable())
            return Task.FromResult(ProviderAvailability.Ready);
        if (ExternalToolDetector.TryFindLibreOfficeSoffice(out _))
            return Task.FromResult(ProviderAvailability.Ready);

        return Task.FromResult(ProviderAvailability.NotReady(
            "Microsoft Word 또는 LibreOffice가 설치되어 있어야 합니다.",
            Capability.ExternalDependencies));
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var tempPdf = Path.Combine(Path.GetTempPath(),
            $"e2j_{Guid.NewGuid():N}_{Path.GetFileNameWithoutExtension(sourcePath)}.pdf");

        try
        {
            progress?.Report(0.05);

            var converted = false;
            string? failureReason = null;

            if (ExternalToolDetector.TryFindLibreOfficeSoffice(out var soffice))
            {
                converted = await ConvertWithLibreOfficeAsync(soffice, sourcePath, tempPdf, cancellationToken)
                    .ConfigureAwait(false);
                if (!converted) failureReason = "LibreOffice 변환에 실패했습니다.";
            }

            if (!converted && ExternalToolDetector.IsWordComAvailable())
            {
                try
                {
                    converted = ConvertWithWordCom(sourcePath, tempPdf);
                    if (!converted) failureReason = "Microsoft Word 변환에 실패했습니다.";
                }
                catch (Exception ex)
                {
                    failureReason = $"Microsoft Word 변환 오류: {ex.Message}";
                }
            }

            if (!converted)
                return ConvertResult.Fail(sourcePath, failureReason ?? "DOCX → PDF 외부 변환 도구가 필요합니다.");

            progress?.Report(0.55);

            var inner = new Progress<double>(p => progress?.Report(0.55 + p * 0.45));
            return _pdfProvider.ConvertCore(tempPdf, outputDirectory, options, inner, cancellationToken)
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

    private static bool ConvertWithWordCom(string sourcePath, string targetPdf)
    {
        const int wdFormatPDF = 17;
        var wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType is null) return false;

        dynamic? word = Activator.CreateInstance(wordType);
        if (word is null) return false;
        try
        {
            word.Visible = false;
            word.DisplayAlerts = 0;
            dynamic doc = word.Documents.Open(sourcePath, ReadOnly: true, Visible: false);
            try
            {
                doc.SaveAs2(targetPdf, wdFormatPDF);
            }
            finally
            {
                doc.Close(false);
            }
            return File.Exists(targetPdf);
        }
        finally
        {
            try { word.Quit(); } catch { }
        }
    }
}
