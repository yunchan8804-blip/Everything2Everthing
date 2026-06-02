using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Everything2Everything.Core.Providers;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Everything2Everything.Core.Converters;

public sealed class OcrProvider : IConverterProvider
{
    private static readonly string[] OcrInputs =
        { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp", ".gif", ".heic", ".heif", ".pdf" };

    private static readonly string[] OcrOutputs = { ".txt", ".docx" };

    private readonly PdfProvider _pdfProvider;

    public OcrProvider() : this(new PdfProvider()) { }

    public OcrProvider(PdfProvider pdfProvider)
    {
        _pdfProvider = pdfProvider;
    }

    public ProviderCapability Capability { get; } = new(
        Id: "ocr",
        DisplayName: "OCR (이미지/PDF → 텍스트·DOCX)",
        SupportedConversions: ProviderCapability.PairsFromMatrix(OcrInputs, OcrOutputs, LossClass.Rasterize),
        Status: ProviderStatus.Available,
        Summary: "Windows OCR 엔진으로 이미지 또는 PDF 페이지에서 텍스트를 추출해 .txt 또는 .docx로 저장합니다.",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: "Windows에 설치된 OCR 언어 팩을 사용 — 한국어/영어는 Windows 11 기본 포함.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var langs = OcrEngine.AvailableRecognizerLanguages;
            if (langs is null || langs.Count == 0)
                return Task.FromResult(ProviderAvailability.NotReady(
                    "Windows OCR 언어 팩이 설치되어 있지 않습니다. 설정 → 시간 및 언어 → 언어에서 OCR 기능을 추가하세요."));
            return Task.FromResult(ProviderAvailability.Ready);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ProviderAvailability.NotReady("Windows OCR 엔진 초기화 실패: " + ex.Message));
        }
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
        var inputExt = Path.GetExtension(sourcePath).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        var pages = inputExt == ".pdf"
            ? await ExtractPdfPagesAsync(sourcePath, options, progress, cancellationToken).ConfigureAwait(false)
            : new List<string> { sourcePath };

        if (pages.Count == 0)
            return ConvertResult.Fail(sourcePath, "OCR 입력 페이지를 추출하지 못했습니다.");

        try
        {
            var engine = ResolveEngine(options.Ocr.Language);
            if (engine is null)
                return ConvertResult.Fail(sourcePath,
                    $"요청한 언어({options.Ocr.Language})에 맞는 OCR 엔진을 찾을 수 없습니다. 사용 가능: {string.Join(", ", OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag))}");

            var pageTexts = new List<string>(pages.Count);
            for (var i = 0; i < pages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await RecognizeAsync(engine, pages[i], cancellationToken).ConfigureAwait(false);
                pageTexts.Add(text);
                progress?.Report((i + 1.0) / pages.Count * 0.9);
            }

            var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
            if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
                return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

            if (outExt == ".txt")
            {
                var combined = pages.Count == 1
                    ? pageTexts[0]
                    : string.Join(Environment.NewLine + Environment.NewLine + "---" + Environment.NewLine + Environment.NewLine, pageTexts);
                await File.WriteAllTextAsync(path, combined, System.Text.Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            else if (outExt == ".docx")
            {
                WriteDocx(path, pageTexts);
            }
            else
            {
                return ConvertResult.Fail(sourcePath, $"지원하지 않는 출력 형식: {outExt}");
            }

            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { path });
        }
        finally
        {
            if (inputExt == ".pdf")
            {
                foreach (var p in pages)
                {
                    try { if (File.Exists(p)) File.Delete(p); } catch { }
                }
            }
        }
    }

    private static OcrEngine? ResolveEngine(string requestedLanguage)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguage)
            || string.Equals(requestedLanguage, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }

        var preferences = requestedLanguage.Split(new[] { '+', ',', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var available = OcrEngine.AvailableRecognizerLanguages;
        foreach (var pref in preferences)
        {
            var matched = available.FirstOrDefault(l =>
                l.LanguageTag.StartsWith(pref, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
                return OcrEngine.TryCreateFromLanguage(matched);
        }

        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    private static async Task<string> RecognizeAsync(OcrEngine engine, string imagePath, CancellationToken cancellationToken)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        memory.Position = 0;

        using var randomAccess = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(randomAccess.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(memory.ToArray());
            await writer.StoreAsync();
        }
        randomAccess.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(randomAccess);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();

        var ocrResult = await engine.RecognizeAsync(bitmap);
        return ocrResult?.Text ?? string.Empty;
    }

    private async Task<List<string>> ExtractPdfPagesAsync(
        string pdfPath,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"e2e_ocr_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var renderOptions = new ConvertOptions
        {
            OutputLocation = OutputLocation.Custom,
            CustomOutputDirectory = tempDir,
            OnCollision = NameCollision.Overwrite,
            PdfRender = new PdfRenderOptions { Dpi = Math.Max(150, options.PdfRender.Dpi) },
        };

        var inner = new Progress<double>(p => progress?.Report(p * 0.4));
        var result = _pdfProvider.ConvertCore(pdfPath, tempDir, ".png", renderOptions, inner, cancellationToken);
        if (result.Status != ConvertStatus.Success)
            return new List<string>();

        return result.OutputPaths.ToList();
    }

    private static void WriteDocx(string path, IReadOnlyList<string> pageTexts)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        for (var pageIndex = 0; pageIndex < pageTexts.Count; pageIndex++)
        {
            var pageText = pageTexts[pageIndex] ?? string.Empty;
            foreach (var line in pageText.Split('\n', StringSplitOptions.None))
            {
                var paragraph = body.AppendChild(new Paragraph());
                var run = paragraph.AppendChild(new Run());
                run.AppendChild(new Text(line.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve });
            }

            if (pageIndex < pageTexts.Count - 1)
            {
                var pageBreakPara = body.AppendChild(new Paragraph());
                var pageBreakRun = pageBreakPara.AppendChild(new Run());
                pageBreakRun.AppendChild(new Break { Type = BreakValues.Page });
            }
        }

        mainPart.Document.Save();
    }
}
