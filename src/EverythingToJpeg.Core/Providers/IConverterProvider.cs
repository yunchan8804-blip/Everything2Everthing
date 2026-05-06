namespace EverythingToJpeg.Core.Providers;

public interface IConverterProvider
{
    ProviderCapability Capability { get; }

    Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

public sealed record ProviderAvailability(
    bool IsReady,
    string? Reason = null,
    IReadOnlyList<ExternalDependency>? MissingDependencies = null)
{
    public static ProviderAvailability Ready { get; } = new(true);

    public static ProviderAvailability NotReady(string reason, IReadOnlyList<ExternalDependency>? missing = null)
        => new(false, reason, missing);
}
