using Everything2Everything.Core.Providers;
using ImageMagick;

namespace Everything2Everything.Core;

public enum BatchMode
{
    Independent,
    CombineToSingle,
}

public sealed class ConversionEngine
{
    private static readonly HashSet<string> CombinableInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jpe", ".webp", ".avif", ".bmp",
        ".tif", ".tiff", ".gif", ".heic", ".heif", ".psd",
    };

    private static readonly HashSet<string> CombinableOutputs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".tif", ".tiff", ".gif",
    };

    private readonly ProviderRegistry _registry;

    public ConversionEngine(ProviderRegistry registry)
    {
        _registry = registry;
    }

    public ProviderRegistry Providers => _registry;

    public async Task<IReadOnlyList<ConvertResult>> ConvertManyAsync(
        IEnumerable<string> sources,
        string outputExtension,
        ConvertOptions options,
        IProgress<ConvertProgress>? progress = null,
        BatchMode batchMode = BatchMode.Independent,
        CancellationToken cancellationToken = default)
    {
        var sourceList = sources.ToList();

        if (batchMode == BatchMode.CombineToSingle && sourceList.Count > 1)
        {
            if (IsCombineSupported(sourceList, outputExtension, out var unsupportedReason))
            {
                var combined = await CombineAsync(sourceList, outputExtension, options, progress, cancellationToken)
                    .ConfigureAwait(false);
                return new[] { combined };
            }

            return new[] { ConvertResult.Fail(sourceList[0], unsupportedReason ?? "단일 파일 결합을 지원하지 않습니다.") };
        }

        var results = new List<ConvertResult>(sourceList.Count);
        for (var i = 0; i < sourceList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = sourceList[i];
            progress?.Report(new ConvertProgress(i, sourceList.Count, source, 0));

            var result = await ConvertOneAsync(source, outputExtension, options,
                new Progress<double>(p => progress?.Report(new ConvertProgress(i, sourceList.Count, source, p))),
                cancellationToken).ConfigureAwait(false);

            results.Add(result);
            progress?.Report(new ConvertProgress(i + 1, sourceList.Count, source, 1));
        }

        return results;
    }

    public async Task<ConvertResult> ConvertOneAsync(
        string sourcePath,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            return ConvertResult.Fail(sourcePath, "파일을 찾을 수 없습니다.");

        var output = ConversionPair.Normalize(outputExtension);
        var inputExt = ConversionPair.Normalize(Path.GetExtension(sourcePath));

        if (string.Equals(inputExt, output, StringComparison.OrdinalIgnoreCase))
            return ConvertResult.Skip(sourcePath, "입력과 출력 형식이 동일해 변환이 필요하지 않습니다.");

        if (!_registry.TryGet(sourcePath, output, out var provider) || provider is null)
        {
            var available = _registry.OutputsForFile(sourcePath);
            var hint = available.Count > 0
                ? $" 가능한 출력: {string.Join(", ", available)}"
                : string.Empty;
            return ConvertResult.Fail(sourcePath, $"{inputExt} → {output} 변환을 지원하지 않습니다.{hint}");
        }

        var availability = await provider.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (!availability.IsReady)
        {
            var missing = availability.MissingDependencies?.Select(d => d.Name) ?? Array.Empty<string>();
            var detail = availability.Reason ?? "필수 의존성이 준비되지 않았습니다.";
            if (missing.Any()) detail += $" (필요: {string.Join(", ", missing)})";
            return ConvertResult.Fail(sourcePath, detail);
        }

        var outputDir = ResolveOutputDirectory(sourcePath, output, options);
        Directory.CreateDirectory(outputDir);

        try
        {
            return await provider.ConvertAsync(sourcePath, outputDir, output, options, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConvertResult.Fail(sourcePath, ex.Message, ex);
        }
    }

    public static bool CanCombine(string outputExtension)
        => CombinableOutputs.Contains(ConversionPair.Normalize(outputExtension));

    public static bool CanCombineInput(string sourcePath)
        => CombinableInputs.Contains(Path.GetExtension(sourcePath).ToLowerInvariant());

    private static bool IsCombineSupported(
        IReadOnlyList<string> sources,
        string outputExtension,
        out string? unsupportedReason)
    {
        var outExt = ConversionPair.Normalize(outputExtension);
        if (!CombinableOutputs.Contains(outExt))
        {
            unsupportedReason = $"단일 파일 결합은 {string.Join(", ", CombinableOutputs.OrderBy(e => e))}만 지원합니다.";
            return false;
        }

        foreach (var path in sources)
        {
            if (!File.Exists(path))
            {
                unsupportedReason = $"파일을 찾을 수 없습니다: {path}";
                return false;
            }
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!CombinableInputs.Contains(ext))
            {
                unsupportedReason = $"단일 파일 결합은 이미지 입력만 지원합니다 (지원 외: {ext}).";
                return false;
            }
        }

        unsupportedReason = null;
        return true;
    }

    private async Task<ConvertResult> CombineAsync(
        IReadOnlyList<string> sources,
        string outputExtension,
        ConvertOptions options,
        IProgress<ConvertProgress>? progress,
        CancellationToken cancellationToken)
    {
        var outExt = ConversionPair.Normalize(outputExtension);
        var firstSource = sources[0];
        var outputDir = ResolveOutputDirectory(firstSource, outExt, options);
        Directory.CreateDirectory(outputDir);

        var baseName = sources.Count == 1
            ? Path.GetFileNameWithoutExtension(firstSource)
            : $"combined_{sources.Count}files_{DateTime.Now:yyyyMMdd_HHmmss}";

        var path = OutputPathHelper.ResolveOutputPath(outputDir, baseName, null, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
            return ConvertResult.Skip(firstSource, "기존 파일이 있어 건너뜁니다.");

        try
        {
            await Task.Run(() =>
            {
                using var collection = new MagickImageCollection();
                for (var i = 0; i < sources.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new ConvertProgress(i, sources.Count, sources[i], 0.5));
                    var image = LoadImageForCombine(sources[i], outExt, options);
                    collection.Add(image);
                    progress?.Report(new ConvertProgress(i, sources.Count, sources[i], 1));
                }

                ApplyCombineEncoding(collection, outExt, options);
                collection.Write(path);
            }, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ConvertProgress(sources.Count, sources.Count, path, 1));
            return ConvertResult.Ok(firstSource, new[] { path });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConvertResult.Fail(firstSource, ex.Message, ex);
        }
    }

    private static MagickImage LoadImageForCombine(string sourcePath, string outputExtension, ConvertOptions options)
    {
        var image = new MagickImage(sourcePath);
        try { image.AutoOrient(); } catch { }

        var alphaCapable = outputExtension is ".tif" or ".tiff";
        if ((!alphaCapable || options.FlattenTransparency) && image.HasAlpha)
        {
            image.BackgroundColor = new MagickColor(options.TransparencyBackground);
            image.Alpha(AlphaOption.Remove);
            image.Alpha(AlphaOption.Off);
        }

        if (options.MaxLongEdgePixels is int maxLong && maxLong > 0
            && (image.Width > (uint)maxLong || image.Height > (uint)maxLong))
        {
            image.Resize(new MagickGeometry((uint)maxLong, (uint)maxLong) { IgnoreAspectRatio = false });
        }

        return image;
    }

    private static void ApplyCombineEncoding(MagickImageCollection collection, string outputExtension, ConvertOptions options)
    {
        foreach (var image in collection)
        {
            switch (outputExtension)
            {
                case ".pdf":
                    image.Format = MagickFormat.Pdf;
                    break;
                case ".tif":
                case ".tiff":
                    image.Format = MagickFormat.Tiff;
                    if (!string.IsNullOrWhiteSpace(options.Tiff.Compression))
                        image.Settings.SetDefine(MagickFormat.Tiff, "compression", options.Tiff.Compression);
                    break;
                case ".gif":
                    image.Format = MagickFormat.Gif;
                    break;
            }
        }
    }

    private static string ResolveOutputDirectory(string sourcePath, string outputExtension, ConvertOptions options)
    {
        var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath))
            ?? throw new InvalidOperationException("소스 경로에서 폴더를 결정할 수 없습니다.");

        return options.OutputLocation switch
        {
            OutputLocation.SameFolderAsSource => sourceDir,
            OutputLocation.Custom => string.IsNullOrWhiteSpace(options.CustomOutputDirectory)
                ? sourceDir
                : options.CustomOutputDirectory!,
            _ => Path.Combine(sourceDir,
                Path.GetFileNameWithoutExtension(sourcePath) + ResolveSubfolderSuffix(outputExtension, options)),
        };
    }

    private static string ResolveSubfolderSuffix(string outputExtension, ConvertOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SubfolderSuffix) && options.SubfolderSuffix != "_converted")
            return options.SubfolderSuffix;

        var ext = outputExtension.TrimStart('.').ToLowerInvariant();
        return string.IsNullOrEmpty(ext) ? "_converted" : "_" + ext;
    }
}

public sealed record ConvertProgress(int Index, int Total, string CurrentPath, double FileProgress);
