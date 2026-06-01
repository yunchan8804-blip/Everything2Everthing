using Everything2Everything.Core.Providers;
using SkiaSharp;
using Svg.Skia;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// SVG 벡터를 PNG 래스터 또는 PDF로 렌더링한다. svg→jpg/webp/bmp/tiff 등은
/// 그래프가 svg→png→X 로 자동 합성하므로 여기선 png·pdf 원자 엣지만 둔다. 순수 .NET(SkiaSharp).
/// </summary>
public sealed class VectorProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "vector",
        DisplayName: "벡터 (SVG)",
        SupportedConversions: new[]
        {
            new ConversionPair(".svg", ".png", LossClass.Rasterize),
            new ConversionPair(".svg", ".pdf", LossClass.Recode),
        },
        Status: ProviderStatus.Available,
        Summary: "SVG를 PNG로 래스터화하거나 PDF로 렌더링합니다 (다른 이미지 포맷은 PNG를 거쳐 자동 변환).",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
        => Task.Run(() => Convert(sourcePath, outputDirectory, outputExtension, options, progress, cancellationToken), cancellationToken);

    private static ConvertResult Convert(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(outPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        progress?.Report(0.1);

        using var svg = new SKSvg();
        var picture = svg.Load(sourcePath);
        if (picture is null)
            return ConvertResult.Fail(sourcePath, "SVG를 읽지 못했습니다.");

        var rect = picture.CullRect;
        var srcW = rect.Width > 0 ? rect.Width : 512f;
        var srcH = rect.Height > 0 ? rect.Height : 512f;

        // MaxLongEdgePixels에 맞춰 스케일 (png만 의미 — pdf는 벡터 보존)
        var scale = 1f;
        if (outExt == ".png" && options.MaxLongEdgePixels is int maxLong && maxLong > 0)
        {
            var longEdge = Math.Max(srcW, srcH);
            if (longEdge > maxLong) scale = maxLong / longEdge;
        }

        var width = Math.Max(1, (int)Math.Ceiling(srcW * scale));
        var height = Math.Max(1, (int)Math.Ceiling(srcH * scale));
        ct.ThrowIfCancellationRequested();

        var tmp = outPath + ".tmp";
        try
        {
            if (outExt == ".pdf")
            {
                using (var stream = new SKFileWStream(tmp))
                using (var document = SKDocument.CreatePdf(stream))
                {
                    var canvas = document.BeginPage(srcW, srcH);
                    canvas.Clear(SKColors.White);
                    canvas.DrawPicture(picture);
                    document.EndPage();
                    document.Close();
                }
            }
            else // .png
            {
                using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    if (scale != 1f) canvas.Scale(scale);
                    canvas.DrawPicture(picture);
                    canvas.Flush();
                }
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.Create(tmp);
                data.SaveTo(fs);
            }

            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmp, outPath);
        }
        catch (Exception)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 정리 실패 무시 */ }
            throw;
        }

        progress?.Report(1.0);
        return ConvertResult.Ok(sourcePath, new[] { outPath });
    }
}
