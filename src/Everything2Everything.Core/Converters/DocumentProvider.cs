using System.Diagnostics;
using System.Text;
using Everything2Everything.Core.Providers;
using Markdig;

namespace Everything2Everything.Core.Converters;

// 텍스트 문서 5종 매트릭스 (html · hwp · docx · md · txt)
//
// HWP/HWPX는 LibreOffice + H2Orestart로 읽기만 가능. 출력은 불가.
//
// 라우팅:
//   {docx, doc, html, htm, txt, hwp, hwpx} ↔ {docx, html, txt}  → LibreOffice
//   md → html                                                    → Markdig
//   html → md                                                    → ReverseMarkdown
//   md → {docx, txt} = md → html → LibreOffice
//   {docx, html, hwp} → md = → html (LibreOffice/Markdig) → ReverseMarkdown
//   txt ↔ md                                                     → trivial copy
public sealed class DocumentProvider : IConverterProvider
{
    private static readonly string[] Inputs =
        { ".html", ".htm", ".hwp", ".hwpx", ".docx", ".doc", ".md", ".markdown", ".txt" };

    private static readonly string[] Outputs = { ".html", ".docx", ".md", ".txt" };

    public ProviderCapability Capability { get; } = new(
        Id: "document",
        DisplayName: "문서 텍스트 변환 (HTML/HWP/DOCX/MD/TXT)",
        SupportedConversions: ProviderCapability.PairsFromMatrix(Inputs, Outputs),
        Status: ProviderStatus.RequiresExternal,
        Summary: "HTML·HWP·DOCX·Markdown·TXT 사이의 양방향 텍스트 변환 (LibreOffice + Markdig + ReverseMarkdown).",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "LibreOffice",
                Description: "DOCX/HTML/TXT/HWP 변환 엔진. HWP 입력에는 H2Orestart 확장이 함께 필요합니다.",
                DownloadUrl: "https://www.libreoffice.org/download/",
                IsRequired: true),
        },
        RoadmapNote: "HWP/HWPX 출력은 H2Orestart 한계로 미지원.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        // LibreOffice는 docx/html/txt 변환에 필수. md ↔ md/txt만 하는 경우엔 없어도 되지만
        // 일관성을 위해 LibreOffice를 요구. (md → md/txt는 trivial이지만 ConvertAsync 내부에서 처리)
        if (!ExternalToolDetector.TryFindLibreOfficeSoffice(out _))
        {
            return Task.FromResult(ProviderAvailability.NotReady(
                "LibreOffice가 필요합니다 (DOCX/HTML/TXT/HWP 변환에 사용).",
                Capability.ExternalDependencies));
        }
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
        var inExt = ConversionPair.Normalize(Path.GetExtension(sourcePath));
        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        var workDir = Path.Combine(Path.GetTempPath(), $"e2e_doc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            progress?.Report(0.05);
            await RouteAsync(sourcePath, inExt, path, outExt, workDir, cancellationToken).ConfigureAwait(false);
            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { path });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return ConvertResult.Fail(sourcePath, ex.Message, ex);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { }
        }
    }

    private static async Task RouteAsync(
        string sourcePath, string inExt,
        string outputPath, string outExt,
        string workDir,
        CancellationToken ct)
    {
        // Normalize aliases
        var input = inExt switch
        {
            ".markdown" => ".md",
            ".htm" => ".html",
            ".doc" => ".docx",
            ".hwpx" => ".hwp",
            _ => inExt,
        };
        var output = outExt;

        // 동일 형식 (이미 ConversionEngine에서 걸러지지만 안전장치)
        if (input == output)
        {
            File.Copy(sourcePath, outputPath, overwrite: true);
            return;
        }

        // ===== MD 입력 =====
        if (input == ".md")
        {
            switch (output)
            {
                case ".txt":
                    // Markdown은 plain text + 마크업 — LibreOffice가 .md를 모르니 복사로 충분
                    File.Copy(sourcePath, outputPath, overwrite: true);
                    return;
                case ".html":
                    await MdToHtmlAsync(sourcePath, outputPath, ct).ConfigureAwait(false);
                    return;
                case ".docx":
                    var midHtml = Path.Combine(workDir, "mid.html");
                    await MdToHtmlAsync(sourcePath, midHtml, ct).ConfigureAwait(false);
                    await SofficeConvertAsync(midHtml, outputPath, "docx", ct).ConfigureAwait(false);
                    return;
            }
        }

        // ===== HTML 입력 =====
        if (input == ".html")
        {
            switch (output)
            {
                case ".md":
                    await HtmlToMdAsync(sourcePath, outputPath, ct).ConfigureAwait(false);
                    return;
                case ".docx":
                case ".txt":
                    await SofficeConvertAsync(sourcePath, outputPath, output.TrimStart('.'), ct).ConfigureAwait(false);
                    return;
            }
        }

        // ===== TXT 입력 =====
        if (input == ".txt")
        {
            switch (output)
            {
                case ".md":
                    File.Copy(sourcePath, outputPath, overwrite: true);
                    return;
                case ".html":
                    await TxtToHtmlAsync(sourcePath, outputPath, ct).ConfigureAwait(false);
                    return;
                case ".docx":
                    await SofficeConvertAsync(sourcePath, outputPath, "docx", ct).ConfigureAwait(false);
                    return;
            }
        }

        // ===== DOCX 입력 (DOC도 alias로 ".docx") =====
        // (실제 입력 ext가 ".doc"여도 LibreOffice는 동일하게 처리)
        if (input == ".docx")
        {
            switch (output)
            {
                case ".html":
                case ".txt":
                    await SofficeConvertAsync(sourcePath, outputPath, output.TrimStart('.'), ct).ConfigureAwait(false);
                    return;
                case ".md":
                    var midHtml = Path.Combine(workDir, "mid.html");
                    await SofficeConvertAsync(sourcePath, midHtml, "html", ct).ConfigureAwait(false);
                    await HtmlToMdAsync(midHtml, outputPath, ct).ConfigureAwait(false);
                    return;
            }
        }

        // ===== HWP 입력 (HWPX도 alias로 ".hwp") =====
        if (input == ".hwp")
        {
            switch (output)
            {
                case ".html":
                case ".txt":
                case ".docx":
                    await SofficeConvertAsync(sourcePath, outputPath, output.TrimStart('.'), ct).ConfigureAwait(false);
                    return;
                case ".md":
                    var midHtml = Path.Combine(workDir, "mid.html");
                    await SofficeConvertAsync(sourcePath, midHtml, "html", ct).ConfigureAwait(false);
                    await HtmlToMdAsync(midHtml, outputPath, ct).ConfigureAwait(false);
                    return;
            }
        }

        throw new NotSupportedException($"지원하지 않는 변환 라우트: {inExt} → {outExt}");
    }

    private static async Task MdToHtmlAsync(string mdPath, string htmlPath, CancellationToken ct)
    {
        var md = await File.ReadAllTextAsync(mdPath, Encoding.UTF8, ct).ConfigureAwait(false);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var body = Markdown.ToHtml(md, pipeline);
        var fullHtml = $"<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\"><title>{Path.GetFileNameWithoutExtension(mdPath)}</title></head><body>{body}</body></html>";
        await File.WriteAllTextAsync(htmlPath, fullHtml, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task HtmlToMdAsync(string htmlPath, string mdPath, CancellationToken ct)
    {
        var html = await File.ReadAllTextAsync(htmlPath, Encoding.UTF8, ct).ConfigureAwait(false);
        var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config
        {
            UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.PassThrough,
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
        });
        var md = converter.Convert(html);
        await File.WriteAllTextAsync(mdPath, md, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task TxtToHtmlAsync(string txtPath, string htmlPath, CancellationToken ct)
    {
        var txt = await File.ReadAllTextAsync(txtPath, Encoding.UTF8, ct).ConfigureAwait(false);
        var encoded = System.Net.WebUtility.HtmlEncode(txt).Replace("\n", "<br/>\n");
        var fullHtml = $"<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\"><title>{Path.GetFileNameWithoutExtension(txtPath)}</title></head><body><pre style=\"font-family: 'Segoe UI', sans-serif; white-space: pre-wrap;\">{encoded}</pre></body></html>";
        await File.WriteAllTextAsync(htmlPath, fullHtml, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task SofficeConvertAsync(string sourcePath, string targetPath, string outputFormat, CancellationToken ct)
    {
        if (!ExternalToolDetector.TryFindLibreOfficeSoffice(out var soffice))
            throw new InvalidOperationException("LibreOffice를 찾을 수 없습니다.");

        var outDir = Path.GetDirectoryName(Path.GetFullPath(targetPath))!;
        Directory.CreateDirectory(outDir);

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
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add(outputFormat);
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(sourcePath);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("LibreOffice 프로세스 시작 실패");

        try { await proc.WaitForExitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { try { proc.Kill(true); } catch { } throw; }

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"LibreOffice 변환 실패 (exit {proc.ExitCode})");

        var produced = Path.Combine(outDir, Path.GetFileNameWithoutExtension(sourcePath) + "." + outputFormat);
        if (!File.Exists(produced))
            throw new FileNotFoundException("LibreOffice가 결과물을 생성하지 않았습니다.", produced);

        if (!string.Equals(produced, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(produced, targetPath);
        }
    }
}
