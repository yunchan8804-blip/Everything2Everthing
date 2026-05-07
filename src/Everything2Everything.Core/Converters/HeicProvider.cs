using Everything2Everything.Core.Providers;
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;

namespace Everything2Everything.Core.Converters;

public sealed class HeicProvider : IConverterProvider
{
    private static readonly string[] HeicInputs = { ".heic", ".heif" };

    private static readonly string[] PassThroughOutputs =
        { ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".tif", ".tiff", ".gif" };

    private static int _codecConfigured;
    private readonly MagickProvider _magickProvider;

    public HeicProvider() : this(new MagickProvider()) { }

    public HeicProvider(MagickProvider magickProvider)
    {
        _magickProvider = magickProvider;
    }

    public ProviderCapability Capability { get; } = new(
        Id: "heic",
        DisplayName: "HEIC / HEIF",
        SupportedConversions: ProviderCapability.PairsFromMatrix(HeicInputs, PassThroughOutputs),
        Status: ProviderStatus.Available,
        Summary: "iPhone 등에서 만든 HEIC·HEIF 사진을 PNG/JPEG/WebP/AVIF/BMP/TIFF/GIF로 변환합니다.",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        EnsureCodec();
        return Task.FromResult(ProviderAvailability.Ready);
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        EnsureCodec();

        var tempPng = Path.Combine(Path.GetTempPath(),
            $"e2e_{Guid.NewGuid():N}_{Path.GetFileNameWithoutExtension(sourcePath)}.png");

        try
        {
            await Task.Run(() =>
            {
                MagicImageProcessor.ProcessImage(sourcePath, tempPng, ProcessImageSettings.Default);
            }, cancellationToken).ConfigureAwait(false);

            progress?.Report(0.5);

            var inner = new Progress<double>(p => progress?.Report(0.5 + p * 0.5));
            var result = await _magickProvider
                .ConvertAsync(tempPng, outputDirectory, outputExtension, options, inner, cancellationToken)
                .ConfigureAwait(false);

            return result with { SourcePath = sourcePath };
        }
        finally
        {
            try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch { }
        }
    }

    private static void EnsureCodec()
    {
        if (Interlocked.Exchange(ref _codecConfigured, 1) == 1) return;
        CodecManager.Configure(codecs => codecs.UseLibheif());
    }
}
