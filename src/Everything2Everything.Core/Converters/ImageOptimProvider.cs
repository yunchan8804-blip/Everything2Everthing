using Everything2Everything.Core.Providers;
using ImageMagick;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// 동일 포맷 이미지를 재인코딩해 용량을 최적화한다(PdfToolProvider의 이미지 버전).
/// 명시적 self-edge(jpg→jpg 등)로 그래프에 등록되며, 출력이 입력보다 커지면 원본을 보존한다.
/// </summary>
public sealed class ImageOptimProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "image-optim",
        DisplayName: "이미지 최적화 (재압축)",
        SupportedConversions: new[]
        {
            new ConversionPair(".jpg", ".jpg", LossClass.Recode),
            new ConversionPair(".jpeg", ".jpeg", LossClass.Recode),
            new ConversionPair(".png", ".png", LossClass.Container),
            new ConversionPair(".webp", ".webp", LossClass.Recode),
            new ConversionPair(".tif", ".tif", LossClass.Container),
            new ConversionPair(".tiff", ".tiff", LossClass.Container),
            new ConversionPair(".bmp", ".bmp", LossClass.Container),
        },
        Status: ProviderStatus.Available,
        Summary: "JPEG/PNG/WebP/TIFF/BMP를 같은 포맷으로 재압축해 용량을 줄입니다 (품질 옵션 적용).",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
        => Task.Run(() => Optimize(sourcePath, outputDirectory, outputExtension, options, progress, cancellationToken), cancellationToken);

    private static ConvertResult Optimize(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, "_optimized", outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(outPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        progress?.Report(0.1);
        var tmp = outPath + ".tmp";
        try
        {
            using (var image = new MagickImage(sourcePath))
            {
                try { image.AutoOrient(); } catch { /* orient 실패 무시 */ }

                if (options.MaxLongEdgePixels is int maxLong && maxLong > 0
                    && (image.Width > (uint)maxLong || image.Height > (uint)maxLong))
                {
                    image.Resize(new MagickGeometry((uint)maxLong, (uint)maxLong) { IgnoreAspectRatio = false });
                }

                ApplyEncoding(image, outExt, options);
                ct.ThrowIfCancellationRequested();
                image.Write(tmp);
            }

            progress?.Report(0.9);

            // 재인코딩이 더 커지면 원본을 사용
            var before = new FileInfo(sourcePath).Length;
            var after = new FileInfo(tmp).Length;
            if (after >= before)
            {
                File.Copy(sourcePath, outPath, overwrite: true);
                File.Delete(tmp);
            }
            else
            {
                if (File.Exists(outPath)) File.Delete(outPath);
                File.Move(tmp, outPath);
            }
        }
        catch (Exception)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 정리 실패 무시 */ }
            throw;
        }

        progress?.Report(1.0);
        return ConvertResult.Ok(sourcePath, new[] { outPath });
    }

    private static void ApplyEncoding(IMagickImage<ushort> image, string outExt, ConvertOptions options)
    {
        switch (outExt)
        {
            case ".jpg":
            case ".jpeg":
                image.Quality = (uint)Math.Clamp(options.Jpeg.Quality, 1, 100);
                image.Format = MagickFormat.Jpeg;
                break;
            case ".png":
                image.Settings.SetDefine(MagickFormat.Png, "compression-level",
                    Math.Clamp(options.Png.Compression, 0, 9).ToString());
                image.Format = MagickFormat.Png;
                break;
            case ".webp":
                image.Quality = (uint)Math.Clamp(options.Webp.Quality, 1, 100);
                if (options.Webp.Lossless)
                    image.Settings.SetDefine(MagickFormat.WebP, "lossless", "true");
                image.Format = MagickFormat.WebP;
                break;
            case ".tif":
            case ".tiff":
                if (!string.IsNullOrWhiteSpace(options.Tiff.Compression))
                    image.Settings.SetDefine(MagickFormat.Tiff, "compression", options.Tiff.Compression);
                image.Format = MagickFormat.Tiff;
                break;
            case ".bmp":
                image.Format = MagickFormat.Bmp;
                break;
        }
    }
}
