using EverythingToJpeg.Core.Providers;
using ImageMagick;

namespace EverythingToJpeg.Core.Converters;

public sealed class MagickProvider : IConverterProvider
{
    private static readonly string[] SingleFrameExtensions =
    {
        ".png", ".bmp", ".jpg", ".jpeg", ".jpe", ".webp", ".avif", ".psd",
        ".dng", ".nef", ".cr2", ".cr3", ".arw", ".raf", ".orf", ".rw2", ".srw", ".pef", ".raw",
    };

    private static readonly string[] MultiFrameExtensions = { ".gif", ".tif", ".tiff" };

    public ProviderCapability Capability { get; } = new(
        Id: "magick",
        DisplayName: "이미지·RAW·애니메이션",
        Extensions: SingleFrameExtensions.Concat(MultiFrameExtensions).ToList(),
        Status: ProviderStatus.Available,
        Summary: "PNG, BMP, JPEG, WebP, AVIF, PSD, GIF, TIFF, RAW(NEF/CR2/ARW/DNG/RAF/ORF/RW2 등)을 JPEG로 변환합니다.",
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

    private static ConvertResult ConvertCore(
        string sourcePath,
        string outputDirectory,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var isMultiFrame = MultiFrameExtensions.Contains(ext);

        if (isMultiFrame)
        {
            using var collection = new MagickImageCollection(sourcePath);
            if (collection.Count == 0)
                return ConvertResult.Fail(sourcePath, "이미지 프레임을 읽지 못했습니다.");

            if (collection.Count == 1)
            {
                var single = collection[0];
                ApplyCommonTransforms(single, options);
                var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, options.OnCollision);
                if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
                    return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");
                WriteJpeg(single, path, options.Quality);
                progress?.Report(1.0);
                return ConvertResult.Ok(sourcePath, new[] { path });
            }

            collection.Coalesce();
            var outputs = new List<string>();
            var width = (int)Math.Ceiling(Math.Log10(collection.Count + 1));
            for (var i = 0; i < collection.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = collection[i];
                ApplyCommonTransforms(frame, options);
                var suffix = $"_{(i + 1).ToString().PadLeft(width, '0')}";
                var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, suffix, options.OnCollision);
                if (OutputPathHelper.ShouldSkip(path, options.OnCollision)) continue;
                WriteJpeg(frame, path, options.Quality);
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
            ApplyCommonTransforms(image, options);
            var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, options.OnCollision);
            if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
                return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");
            WriteJpeg(image, path, options.Quality);
            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { path });
        }
    }

    private static void ApplyCommonTransforms(IMagickImage<ushort> image, ConvertOptions options)
    {
        try { image.AutoOrient(); } catch { }

        if (options.FlattenTransparency && image.HasAlpha)
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

        image.Format = MagickFormat.Jpeg;
    }

    private static void WriteJpeg(IMagickImage<ushort> image, string path, int quality)
    {
        image.Quality = (uint)Math.Clamp(quality, 1, 100);
        image.Format = MagickFormat.Jpeg;
        image.Write(path);
    }
}
