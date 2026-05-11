using System.Diagnostics;
using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// LibreOffice의 PDF import 필터를 이용해 PDF를 편집 가능한 DOCX로 변환합니다.
/// 레이아웃이 완벽하지 않을 수 있지만, OCR과 달리 텍스트 선택 가능한 PDF의
/// 원본 텍스트·표·이미지를 최대한 보존합니다.
/// </summary>
public sealed class PdfToDocxProvider : IConverterProvider
{
    private static readonly string[] PdfInputs = { ".pdf" };
    private static readonly string[] DocxOutputs = { ".docx" };

    public ProviderCapability Capability { get; } = new(
        Id: "pdf-to-docx",
        DisplayName: "PDF → Word (DOCX)",
        SupportedConversions: ProviderCapability.PairsFromMatrix(PdfInputs, DocxOutputs),
        Status: ProviderStatus.RequiresExternal,
        Summary: "LibreOffice의 PDF import를 이용해 PDF를 편집 가능한 DOCX로 변환합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "LibreOffice",
                Description: "PDF → DOCX 변환에 LibreOffice가 필요합니다.",
                DownloadUrl: "https://www.libreoffice.org/download/",
                IsRequired: true),
        },
        RoadmapNote: "레이아웃 보존도는 LibreOffice PDF import 품질에 의존합니다.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (ExternalToolDetector.TryFindLibreOfficeSoffice(out _))
            return Task.FromResult(ProviderAvailability.Ready);

        return Task.FromResult(ProviderAvailability.NotReady(
            "LibreOffice가 설치되어 있어야 합니다.",
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
        if (!ExternalToolDetector.TryFindLibreOfficeSoffice(out var soffice))
            return ConvertResult.Fail(sourcePath, "LibreOffice가 필요합니다.");

        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var finalPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, ".docx", options.OnCollision);
        if (OutputPathHelper.ShouldSkip(finalPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        progress?.Report(0.1);

        // LibreOffice: PDF → DOCX (Draw import filter 사용)
        var tempDir = Path.Combine(Path.GetTempPath(), $"e2e_pdf2docx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = soffice,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("--headless");
            psi.ArgumentList.Add("--norestore");
            psi.ArgumentList.Add("--nofirststartwizard");
            psi.ArgumentList.Add("--infilter=writer_pdf_import");
            psi.ArgumentList.Add("--convert-to");
            psi.ArgumentList.Add("docx");
            psi.ArgumentList.Add("--outdir");
            psi.ArgumentList.Add(tempDir);
            psi.ArgumentList.Add(sourcePath);

            using var proc = Process.Start(psi);
            if (proc is null)
                return ConvertResult.Fail(sourcePath, "LibreOffice 프로세스를 시작할 수 없습니다.");

            try
            {
                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(true); } catch { }
                throw;
            }

            progress?.Report(0.8);

            if (proc.ExitCode != 0)
                return ConvertResult.Fail(sourcePath, "LibreOffice PDF → DOCX 변환에 실패했습니다.");

            var produced = Path.Combine(tempDir, baseName + ".docx");
            if (!File.Exists(produced))
                return ConvertResult.Fail(sourcePath, "LibreOffice가 DOCX 파일을 생성하지 못했습니다.");

            File.Copy(produced, finalPath, overwrite: options.OnCollision == NameCollision.Overwrite);
            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { finalPath });
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }
}
