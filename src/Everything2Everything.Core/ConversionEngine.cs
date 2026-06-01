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

        // 그래프 경로 탐색: 직접 엣지가 있으면 1홉, 없으면 손실 가중치 기반 멀티홉을 자동 합성.
        var maxHops = options.AllowMultiHop ? Math.Max(1, options.MaxHops) : 1;
        var path = _registry.Graph.FindBestPath(inputExt, output, maxHops, !options.AvoidLossy);

        if (path is null || path.Count == 0)
        {
            if (string.Equals(inputExt, output, StringComparison.OrdinalIgnoreCase))
                return ConvertResult.Skip(sourcePath, "입력과 출력 형식이 동일해 변환이 필요하지 않습니다.");

            var available = _registry.OutputsForFile(sourcePath);
            var hint = available.Count > 0
                ? $" 가능한 출력: {string.Join(", ", available)}"
                : string.Empty;
            return ConvertResult.Fail(sourcePath, $"{inputExt} → {output} 변환을 지원하지 않습니다.{hint}");
        }

        return await ExecuteChainAsync(sourcePath, output, path, options, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 변환 경로(1홉 또는 멀티홉)를 순차 실행한다. 중간 산출물은 임시 작업폴더에 체이닝하고
    /// 마지막 홉만 실제 출력 폴더에 쓴다. 각 홉 시작 전 Provider 가용성을 점검한다.
    /// </summary>
    private async Task<ConvertResult> ExecuteChainAsync(
        string sourcePath,
        string finalOutputExtension,
        IReadOnlyList<ConversionGraph.Edge> path,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        foreach (var edge in path)
        {
            var availability = await edge.Provider.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);
            if (!availability.IsReady)
            {
                var missing = availability.MissingDependencies?.Select(d => d.Name) ?? Array.Empty<string>();
                var detail = availability.Reason ?? "필수 의존성이 준비되지 않았습니다.";
                if (missing.Any()) detail += $" (필요: {string.Join(", ", missing)})";
                return ConvertResult.Fail(sourcePath, detail);
            }
        }

        // 단일 홉 — 기존 동작과 동일 (출력 폴더 해결 후 직접 위임)
        if (path.Count == 1)
        {
            var outputDir = ResolveOutputDirectory(sourcePath, finalOutputExtension, options);
            Directory.CreateDirectory(outputDir);
            try
            {
                return await path[0].Provider
                    .ConvertAsync(sourcePath, outputDir, finalOutputExtension, options, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return ConvertResult.Fail(sourcePath, ex.Message, ex); }
        }

        // 멀티홉 — 중간 산출물은 임시 폴더, 마지막만 실제 출력
        var workRoot = Path.Combine(Path.GetTempPath(), "e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workRoot);
        try
        {
            var current = sourcePath;
            for (var h = 0; h < path.Count; h++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var edge = path[h];
                var isLast = h == path.Count - 1;
                var hopExt = edge.To;
                var outDir = isLast
                    ? ResolveOutputDirectory(sourcePath, finalOutputExtension, options)
                    : Path.Combine(workRoot, "h" + h.ToString());
                Directory.CreateDirectory(outDir);

                var hopIndex = h;
                var hopProgress = new Progress<double>(p =>
                    progress?.Report((hopIndex + Math.Clamp(p, 0, 1)) / path.Count));

                ConvertResult result;
                try
                {
                    result = await edge.Provider
                        .ConvertAsync(current, outDir, hopExt, options, hopProgress, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return ConvertResult.Fail(sourcePath,
                        $"{h + 1}/{path.Count}단계 ({edge.From} → {edge.To}) 실패: {ex.Message}", ex);
                }

                if (isLast)
                {
                    // 마지막 홉은 상태를 그대로 존중 (정상 Skip을 Fail로 둔갑시키지 않음)
                    return result.Status switch
                    {
                        ConvertStatus.Success => ConvertResult.Ok(sourcePath, result.OutputPaths),
                        ConvertStatus.Skipped => ConvertResult.Skip(sourcePath, result.Message ?? "건너뛰었습니다."),
                        _ => ConvertResult.Fail(sourcePath,
                            $"{h + 1}/{path.Count}단계 ({edge.From} → {edge.To}) 실패: {result.Message ?? "산출물이 없습니다."}", result.Error),
                    };
                }

                // 중간 홉은 단일 산출물이어야 다음 홉으로 체이닝할 수 있다.
                if (result.Status != ConvertStatus.Success || result.OutputPaths.Count == 0)
                {
                    return ConvertResult.Fail(sourcePath,
                        $"{h + 1}/{path.Count}단계 ({edge.From} → {edge.To}) 실패: {result.Message ?? "산출물이 없습니다."}");
                }
                if (result.OutputPaths.Count > 1)
                {
                    // 다중 산출물(예: PDF→페이지별 이미지)을 중간 홉으로 두면 나머지가 유실된다 — 명시적 거부.
                    return ConvertResult.Fail(sourcePath,
                        $"{h + 1}/{path.Count}단계 ({edge.From} → {edge.To})가 여러 파일을 생성해 자동 합성된 다음 단계로 이어갈 수 없습니다. 직접 변환 경로를 사용하거나 단계를 나눠 실행하세요.");
                }

                current = result.OutputPaths[0]; // 다음 홉의 입력
            }

            return ConvertResult.Fail(sourcePath, "변환 체인 실행에 실패했습니다.");
        }
        finally
        {
            try { Directory.Delete(workRoot, true); } catch { /* 임시 정리 실패 무시 */ }
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
