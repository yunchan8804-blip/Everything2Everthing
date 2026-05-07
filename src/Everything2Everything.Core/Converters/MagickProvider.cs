using Everything2Everything.Core.Providers;
using ImageMagick;

namespace Everything2Everything.Core.Converters;

public sealed class MagickProvider : IConverterProvider
{
    private static readonly string[] SingleFrameInputs =
    {
        ".png", ".bmp", ".jpg", ".jpeg", ".jpe", ".webp", ".avif", ".psd",
        ".dng", ".nef", ".cr2", ".cr3", ".arw", ".raf", ".orf", ".rw2", ".srw", ".pef", ".raw",
    };

    private static readonly string[] MultiFrameInputs = { ".gif", ".tif", ".tiff" };

    private static readonly string[] AllInputs = SingleFrameInputs.Concat(MultiFrameInputs).ToArray();

    private static readonly string[] WritableOutputs =
        { ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".tif", ".tiff", ".gif", ".pdf" };

    private static readonly HashSet<string> AlphaCapableOutputs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".webp", ".avif", ".tif", ".tiff", ".gif",
    };

    public ProviderCapability Capability { get; } = new(
        Id: "magick",
        DisplayName: "이미지·RAW·애니메이션",
        SupportedConversions: ProviderCapability.PairsFromMatrix(AllInputs, WritableOutputs),
        Status: ProviderStatus.Available,
        Summary: "PNG/JPEG/WebP/AVIF/BMP/TIFF/GIF/PDF 사이의 양방향 변환 + RAW(NEF/CR2/ARW/DNG…)·PSD 디코딩.",
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

    private static ConvertResult ConvertCore(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var inputExt = Path.GetExtension(sourcePath).ToLowerInvariant();
        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var isMultiFrameInput = MultiFrameInputs.Contains(inputExt);
        var format = ResolveFormat(outExt);
        var isMultiFrameOutput = outExt is ".gif" or ".tif" or ".tiff" or ".pdf";

        if (isMultiFrameInput)
        {
            using var collection = new MagickImageCollection(sourcePath);
            if (collection.Count == 0)
                return ConvertResult.Fail(sourcePath, "이미지 프레임을 읽지 못했습니다.");

            if (collection.Count == 1)
            {
                var single = collection[0];
                ApplyCommonTransforms(single, outExt, options);
                var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
                if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
                    return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");
                WriteSingle(single, path, format, outExt, options);
                progress?.Report(1.0);
                return ConvertResult.Ok(sourcePath, new[] { path });
            }

            collection.Coalesce();

            if (isMultiFrameOutput)
            {
                foreach (var frame in collection)
                    ApplyCommonTransforms(frame, outExt, options);
                var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
                if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
                    return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");
                ApplyCollectionEncoding(collection, format, outExt, options);
                collection.Write(path);
                progress?.Report(1.0);
                return ConvertResult.Ok(sourcePath, new[] { path });
            }

            var outputs = new List<string>();
            var width = (int)Math.Ceiling(Math.Log10(collection.Count + 1));
            for (var i = 0; i < collection.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = collection[i];
                ApplyCommonTransforms(frame, outExt, options);
                var suffix = $"_{(i + 1).ToString().PadLeft(width, '0')}";
                var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, suffix, outExt, options.OnCollision);
                if (OutputPathHelper.ShouldSkip(path, options.OnCollision)) continue;
                WriteSingle(frame, path, format, outExt, options);
                outputs.Add(path);
                progress?.Report((i + 1.0) / collection.Count);
            }

            return outputs.Count > 0
                ? ConvertResult.Ok(sourcePath, outputs)
                : ConvertResult.Skip(sourcePath, "모든 프레임이 이미 존재해 건너뜁니다.");
        }
        else
        {
            using var image = new MagickImage(sourcePath);
            ApplyCommonTransforms(image, outExt, options);
            var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
            if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
                return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");
            WriteSingle(image, path, format, outExt, options);
            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { path });
        }
    }

    private static MagickFormat ResolveFormat(string outputExtension) => outputExtension switch
    {
        ".png" => MagickFormat.Png,
        ".jpg" or ".jpeg" => MagickFormat.Jpeg,
        ".webp" => MagickFormat.WebP,
        ".avif" => MagickFormat.Avif,
        ".bmp" => MagickFormat.Bmp,
        ".tif" or ".tiff" => MagickFormat.Tiff,
        ".gif" => MagickFormat.Gif,
        ".pdf" => MagickFormat.Pdf,
        _ => throw new NotSupportedException($"지원하지 않는 출력 형식: {outputExtension}"),
    };

    private static void ApplyCommonTransforms(IMagickImage<ushort> image, string outputExtension, ConvertOptions options)
    {
        try { image.AutoOrient(); } catch { }

        var flattenForOutput = !AlphaCapableOutputs.Contains(outputExtension);
        if ((flattenForOutput || options.FlattenTransparency) && image.HasAlpha)
        {
            image.BackgroundColor = new MagickColor(options.TransparencyBackground);
            image.Alpha(AlphaOption.Remove);
            image.Alpha(AlphaOption.Off);
        }

        if (options.MaxLongEdgePixels is int maxLong && maxLong > 0)
        {
            var w = (int)image.Width;
            var h = (int)image.Height;
            if (w > maxLong || h > maxLong)
            {
                var geom = new MagickGeometry((uint)maxLong, (uint)maxLong) { IgnoreAspectRatio = false };
                image.Resize(geom);
            }
        }
    }

    private static void WriteSingle(IMagickImage<ushort> image, string path, MagickFormat format, string outputExtension, ConvertOptions options)
    {
        ApplySingleEncoding(image, format, outputExtension, options);
        image.Write(path);
    }

    private static void ApplySingleEncoding(IMagickImage<ushort> image, MagickFormat format, string outputExtension, ConvertOptions options)
    {
        image.Format = format;
        switch (outputExtension)
        {
            case ".jpg":
            case ".jpeg":
                image.Quality = (uint)Math.Clamp(options.Jpeg.Quality, 1, 100);
                if (options.Jpeg.Progressive)
                    image.Settings.Interlace = Interlace.Jpeg;
                break;
            case ".png":
                image.Quality = (uint)Math.Clamp((options.Png.Compression * 10) + 5, 1, 100);
                if (options.Png.Interlace)
                    image.Settings.Interlace = Interlace.Png;
                break;
            case ".webp":
                image.Quality = (uint)Math.Clamp(options.Webp.Quality, 1, 100);
                if (options.Webp.Lossless)
                    image.Settings.SetDefine(MagickFormat.WebP, "lossless", "true");
                break;
            case ".avif":
                image.Quality = (uint)Math.Clamp(options.Avif.Quality, 1, 100);
                image.Settings.SetDefine(MagickFormat.Avif, "speed", Math.Clamp(options.Avif.Speed, 0, 10).ToString());
                break;
            case ".tif":
            case ".tiff":
                if (!string.IsNullOrWhiteSpace(options.Tiff.Compression))
                    image.Settings.SetDefine(MagickFormat.Tiff, "compression", options.Tiff.Compression);
                break;
            case ".pdf":
                ApplyPdfPageSettings(image, options);
                break;
        }
    }

    private static void ApplyPdfPageSettings(IMagickImage<ushort> image, ConvertOptions options)
    {
        var pageSize = options.PdfBuild.PageSize;
        if (string.Equals(pageSize, "Auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(pageSize))
        {
            return;
        }

        if (TryGetPagePoints(pageSize, out var widthPt, out var heightPt))
        {
            var marginPt = Math.Max(0, options.PdfBuild.MarginPoints);
            var contentW = (uint)Math.Max(1, widthPt - marginPt * 2);
            var contentH = (uint)Math.Max(1, heightPt - marginPt * 2);

            if (options.PdfBuild.FitToPage)
            {
                var geom = new MagickGeometry(contentW, contentH) { IgnoreAspectRatio = false };
                image.Resize(geom);
            }

            image.Page = new MagickGeometry(
                (int)marginPt, (int)marginPt,
                (uint)widthPt, (uint)heightPt);
        }
    }

    private static bool TryGetPagePoints(string pageSize, out int widthPt, out int heightPt)
    {
        switch (pageSize.ToUpperInvariant())
        {
            case "A4":      widthPt = 595;  heightPt = 842;  return true;
            case "A3":      widthPt = 842;  heightPt = 1191; return true;
            case "A5":      widthPt = 420;  heightPt = 595;  return true;
            case "LETTER":  widthPt = 612;  heightPt = 792;  return true;
            case "LEGAL":   widthPt = 612;  heightPt = 1008; return true;
            default:        widthPt = 0;    heightPt = 0;    return false;
        }
    }

    private static void ApplyCollectionEncoding(MagickImageCollection collection, MagickFormat format, string outputExtension, ConvertOptions options)
    {
        foreach (var img in collection)
            ApplySingleEncoding(img, format, outputExtension, options);
    }
}
