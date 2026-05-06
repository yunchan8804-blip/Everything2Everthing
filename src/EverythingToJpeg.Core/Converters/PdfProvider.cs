using EverythingToJpeg.Core.Providers;
using PDFtoImage;
using SkiaSharp;

namespace EverythingToJpeg.Core.Converters;

public sealed class PdfProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "pdf",
        DisplayName: "PDF",
        Extensions: new[] { ".pdf" },
        Status: ProviderStatus.Available,
        Summary: "PDF 각 페이지를 JPEG로 변환합니다.",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => ConvertCore(sourcePath, outputDirectory, options, progress, cancellationToken), cancellationToken);
    }

    internal ConvertResult ConvertCore(
        string sourcePath,
        string outputDirectory,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        int pageCount;
        using (var probe = File.OpenRead(sourcePath))
        {
            pageCount = Conversion.GetPageCount(probe);
        }
        if (pageCount <= 0)
            return ConvertResult.Fail(sourcePath, "PDF에 페이지가 없습니다.");

        var renderOptions = new RenderOptions
        {
            Dpi = options.PdfDpi,
            BackgroundColor = SKColors.White,
            WithAnnotations = true,
            WithFormFill = true,
            UseTiling = true,
        };

        var width = (int)Math.Ceiling(Math.Log10(pageCount + 1));
        var outputs = new List<string>();

        for (var i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var suffix = pageCount == 1 ? null : $"_p{(i + 1).ToString().PadLeft(width, '0')}";
            var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, suffix, options.OnCollision);
            if (OutputPathHelper.ShouldSkip(path, options.OnCollision)) continue;

            using (var input = File.OpenRead(sourcePath))
            {
                Conversion.SaveJpeg(path, input, page: i, leaveOpen: false, password: null, options: renderOptions);
            }

            if (options.MaxLongEdgePixels is int maxLong && maxLong > 0)
            {
                ResizeIfNeeded(path, maxLong, options.Quality);
            }

            outputs.Add(path);
            progress?.Report((i + 1.0) / pageCount);
        }

        return outputs.Count > 0
            ? ConvertResult.Ok(sourcePath, outputs)
            : ConvertResult.Skip(sourcePath, "모든 페이지가 이미 존재해 건너뜁니다.");
    }

    private static void ResizeIfNeeded(string jpegPath, int maxLongEdge, int quality)
    {
        using var image = new ImageMagick.MagickImage(jpegPath);
        if (image.Width <= (uint)maxLongEdge && image.Height <= (uint)maxLongEdge) return;

        var geom = new ImageMagick.MagickGeometry((uint)maxLongEdge, (uint)maxLongEdge) { IgnoreAspectRatio = false };
        image.Resize(geom);
        image.Quality = (uint)Math.Clamp(quality, 1, 100);
        image.Format = ImageMagick.MagickFormat.Jpeg;
        image.Write(jpegPath);
    }
}
