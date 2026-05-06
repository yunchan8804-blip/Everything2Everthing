namespace EverythingToJpeg.Core.Providers;

public sealed class ProviderRegistry
{
    private readonly List<IConverterProvider> _providers;
    private readonly Dictionary<string, IConverterProvider> _byExtension = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(IEnumerable<IConverterProvider> providers)
    {
        _providers = providers.ToList();
        foreach (var provider in _providers)
        {
            if (!provider.Capability.IsImplemented) continue;
            foreach (var ext in provider.Capability.Extensions)
            {
                _byExtension[Normalize(ext)] = provider;
            }
        }
    }

    public IReadOnlyList<IConverterProvider> All => _providers;

    public IEnumerable<IConverterProvider> Implemented => _providers.Where(p => p.Capability.IsImplemented);

    public IEnumerable<IConverterProvider> ComingSoon => _providers.Where(p => p.Capability.Status == ProviderStatus.ComingSoon);

    public bool TryGetForFile(string sourcePath, out IConverterProvider? provider)
    {
        var ext = Normalize(Path.GetExtension(sourcePath));
        return _byExtension.TryGetValue(ext, out provider);
    }

    public IConverterProvider? FindByExtension(string ext)
        => _byExtension.TryGetValue(Normalize(ext), out var p) ? p : null;

    public IReadOnlyCollection<string> ImplementedExtensions => _byExtension.Keys;

    private static string Normalize(string ext)
        => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
}
