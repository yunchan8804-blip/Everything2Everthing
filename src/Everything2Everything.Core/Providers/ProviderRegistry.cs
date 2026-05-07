namespace Everything2Everything.Core.Providers;

public sealed class ProviderRegistry
{
    private readonly List<IConverterProvider> _providers;
    private readonly Dictionary<(string Input, string Output), IConverterProvider> _byPair
        = new(PairComparer.Instance);
    private readonly Dictionary<string, List<string>> _outputsByInput
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allOutputs = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(IEnumerable<IConverterProvider> providers)
    {
        _providers = providers.ToList();
        foreach (var provider in _providers)
        {
            if (!provider.Capability.IsImplemented) continue;
            foreach (var pair in provider.Capability.SupportedConversions)
            {
                var key = (pair.InputExtension, pair.OutputExtension);
                _byPair.TryAdd(key, provider);
                if (!_outputsByInput.TryGetValue(pair.InputExtension, out var list))
                    _outputsByInput[pair.InputExtension] = list = new List<string>();
                if (!list.Contains(pair.OutputExtension, StringComparer.OrdinalIgnoreCase))
                    list.Add(pair.OutputExtension);
                _allInputs.Add(pair.InputExtension);
                _allOutputs.Add(pair.OutputExtension);
            }
        }
    }

    public IReadOnlyList<IConverterProvider> All => _providers;

    public IEnumerable<IConverterProvider> Implemented => _providers.Where(p => p.Capability.IsImplemented);

    public IEnumerable<IConverterProvider> ComingSoon => _providers.Where(p => p.Capability.Status == ProviderStatus.ComingSoon);

    public bool TryGet(string sourcePath, string outputExtension, out IConverterProvider? provider)
    {
        var input = ConversionPair.Normalize(Path.GetExtension(sourcePath));
        var output = ConversionPair.Normalize(outputExtension);
        return _byPair.TryGetValue((input, output), out provider);
    }

    public IConverterProvider? FindByPair(string inputExtension, string outputExtension)
    {
        var key = (ConversionPair.Normalize(inputExtension), ConversionPair.Normalize(outputExtension));
        return _byPair.TryGetValue(key, out var p) ? p : null;
    }

    public IReadOnlyList<string> OutputsForInput(string inputExtension)
    {
        var input = ConversionPair.Normalize(inputExtension);
        return _outputsByInput.TryGetValue(input, out var list)
            ? list.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();
    }

    public IReadOnlyList<string> OutputsForFile(string sourcePath)
        => OutputsForInput(Path.GetExtension(sourcePath));

    public IReadOnlyCollection<string> AllInputExtensions => _allInputs;

    public IReadOnlyCollection<string> AllOutputExtensions => _allOutputs;

    private sealed class PairComparer : IEqualityComparer<(string, string)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((string, string) x, (string, string) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

        public int GetHashCode((string, string) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1 ?? ""),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2 ?? ""));
    }
}
