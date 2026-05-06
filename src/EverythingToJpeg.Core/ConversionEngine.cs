using EverythingToJpeg.Core.Providers;

namespace EverythingToJpeg.Core;

public sealed class ConversionEngine
{
    private readonly ProviderRegistry _registry;

    public ConversionEngine(ProviderRegistry registry)
    {
        _registry = registry;
    }

    public ProviderRegistry Providers => _registry;

    public async Task<IReadOnlyList<ConvertResult>> ConvertManyAsync(
        IEnumerable<string> sources,
        ConvertOptions options,
        IProgress<ConvertProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sourceList = sources.ToList();
        var results = new List<ConvertResult>(sourceList.Count);

        for (var i = 0; i < sourceList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = sourceList[i];
            progress?.Report(new ConvertProgress(i, sourceList.Count, source, 0));

            var result = await ConvertOneAsync(source, options,
                new Progress<double>(p => progress?.Report(new ConvertProgress(i, sourceList.Count, source, p))),
                cancellationToken).ConfigureAwait(false);

            results.Add(result);
            progress?.Report(new ConvertProgress(i + 1, sourceList.Count, source, 1));
        }

        return results;
    }

    public async Task<ConvertResult> ConvertOneAsync(
        string sourcePath,
        ConvertOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            return ConvertResult.Fail(sourcePath, "파일을 찾을 수 없습니다.");

        if (!_registry.TryGetForFile(sourcePath, out var provider) || provider is null)
            return ConvertResult.Fail(sourcePath, $"지원하지 않는 형식입니다: {Path.GetExtension(sourcePath)}");

        var availability = await provider.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (!availability.IsReady)
        {
            var missing = availability.MissingDependencies?.Select(d => d.Name) ?? Array.Empty<string>();
            var detail = availability.Reason ?? "필수 의존성이 준비되지 않았습니다.";
            if (missing.Any()) detail += $" (필요: {string.Join(", ", missing)})";
            return ConvertResult.Fail(sourcePath, detail);
        }

        var outputDir = ResolveOutputDirectory(sourcePath, options);
        Directory.CreateDirectory(outputDir);

        try
        {
            return await provider.ConvertAsync(sourcePath, outputDir, options, progress, cancellationToken)
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

    private static string ResolveOutputDirectory(string sourcePath, ConvertOptions options)
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
                Path.GetFileNameWithoutExtension(sourcePath) + options.SubfolderSuffix),
        };
    }
}

public sealed record ConvertProgress(int Index, int Total, string CurrentPath, double FileProgress);
