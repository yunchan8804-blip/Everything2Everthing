namespace EverythingToJpeg.Core.Providers;

public enum ProviderStatus
{
    Available,
    Preview,
    RequiresExternal,
    ComingSoon,
    Disabled,
}

public sealed record ExternalDependency(
    string Name,
    string Description,
    string? DownloadUrl = null,
    bool IsRequired = true);

public sealed record ProviderCapability(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Extensions,
    ProviderStatus Status,
    string Summary,
    IReadOnlyList<ExternalDependency> ExternalDependencies,
    string? RoadmapNote = null)
{
    public bool CanRegisterContextMenu => Status is ProviderStatus.Available or ProviderStatus.Preview or ProviderStatus.RequiresExternal;
    public bool IsImplemented => Status is not ProviderStatus.ComingSoon and not ProviderStatus.Disabled;
}
