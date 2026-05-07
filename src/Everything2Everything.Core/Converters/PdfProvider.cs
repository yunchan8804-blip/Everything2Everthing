using Everything2Everything.Core.Providers;
using PDFtoImage;
using SkiaSharp;

namespace Everything2Everything.Core.Converters;

public sealed class PdfProvider : IConverterProvider
{
    private static readonly string[] PdfInputs = { ".pdf" };

    private static readonly string[] PdfRenderOutputs =
        { ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".tif", ".tiff" };

    public ProviderCapability Capability { get; } = new(
        Id: "pdf",
        DisplayName: "PDF",
        SupportedConversions: ProviderCapability.PairsFromMatrix(PdfInputs, PdfRenderOutputs),
        Status: ProviderStatus.Available,
        Summary: "PDF 각 페이지를 PNG/JPEG/WebP/AVIF/BMP/TIFF로 렌더링합니다.",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => ConvertCore(sourcePath, outputDirectory, outputExtension, options, progress, cancellationToken),
            cancellationToken);
    }

    internal ConvertResult ConvertCore(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var outExt = ConversionPair.Normalize(outputExtension);
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
            Dpi = options.PdfRender.Dpi,
            BackgroundColor = SKColors.White,
            WithAnnotations = options.PdfRender.WithAnnotations,
            WithFormFill = options.PdfRender.WithFormFill,
            UseTiling = true,
        };

        var width = (int)Math.Ceiling(Math.Log10(pageCount + 1));
        var outputs = new List<string>();

        for (var i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var suffix = pageCount == 1 ? null : $"_p{(i + 1).ToString().PadLeft(width, '0')}";
            var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, suffix, outExt, options.OnCollision);
            if (OutputPathHelper.ShouldSkip(path, options.OnCollision)) continue;

            using (var input = File.OpenRead(sourcePath))
            using (var pngStream = new MemoryStream())
            {
                Conversion.SavePng(pngStream, input, page: i, leaveOpen: false, password: null, options: renderOptions);
                pngStream.Position = 0;

                using var image = new ImageMagick.MagickImage(pngStream);
                ApplyTransforms(image, outExt, options);
                ApplyEncoding(image, outExt, options);
                image.Write(path);
            }

            outputs.Add(path);
            progress?.Report((i + 1.0) / pageCount);
        }

        return outputs.Count > 0
            ? ConvertResult.Ok(sourcePath, outputs)
            : ConvertResult.Skip(sourcePath, "모든 페이지가 이미 존재해 건너뜁니다.");
    }

    private static void ApplyTransforms(ImageMagick.IMagickImage<ushort> image, string outputExtension, ConvertOptions options)
    {
        var alphaCapable = outputExtension is ".png" or ".webp" or ".avif" or ".tif" or ".tiff";
        if ((!alphaCapable || options.FlattenTransparency) && image.HasAlpha)
        {
            image.BackgroundColor = new ImageMagick.MagickColor(options.TransparencyBackground);
            image.Alpha(ImageMagick.AlphaOption.Remove);
            image.Alpha(ImageMagick.AlphaOption.Off);
        }

        if (options.MaxLongEdgePixels is int maxLong && maxLong > 0
            && (image.Width > (uint)maxLong || image.Height > (uint)maxLong))
        {
            image.Resize(new ImageMagick.MagickGeometry((uint)maxLong, (uint)maxLong) { IgnoreAspectRatio = false });
        }
    }

    private static void ApplyEncoding(ImageMagick.IMagickImage<ushort> image, string outputExtension, ConvertOptions options)
    {
        switch (outputExtension)
        {
            case ".jpg":
            case ".jpeg":
                image.Quality = (uint)Math.Clamp(options.Jpeg.Quality, 1, 100);
                image.Format = ImageMagick.MagickFormat.Jpeg;
                break;
            case ".png":
                image.Format = ImageMagick.MagickFormat.Png;
                break;
            case ".webp":
                image.Quality = (uint)Math.Clamp(options.Webp.Quality, 1, 100);
                if (options.Webp.Lossless)
                    image.Settings.SetDefine(ImageMagick.MagickFormat.WebP, "lossless", "true");
                image.Format = ImageMagick.MagickFormat.WebP;
                break;
            case ".avif":
                image.Quality = (uint)Math.Clamp(options.Avif.Quality, 1, 100);
                image.Settings.SetDefine(ImageMagick.MagickFormat.Avif, "speed", Math.Clamp(options.Avif.Speed, 0, 10).ToString());
                image.Format = ImageMagick.MagickFormat.Avif;
                break;
            case ".bmp":
                image.Format = ImageMagick.MagickFormat.Bmp;
                break;
            case ".tif":
            case ".tiff":
                if (!string.IsNullOrWhiteSpace(options.Tiff.Compression))
                    image.Settings.SetDefine(ImageMagick.MagickFormat.Tiff, "compression", options.Tiff.Compression);
                image.Format = ImageMagick.MagickFormat.Tiff;
                break;
        }
    }
}
