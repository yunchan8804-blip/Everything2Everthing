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
    private readonly ConversionGraph _graph = new();

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
                _graph.AddEdge(pair.InputExtension, pair.OutputExtension, provider, pair.Loss);
            }
        }
    }

    /// <summary>모든 Provider 능력으로부터 빌드된 변환 그래프. 엔진의 멀티홉 경로 탐색에 사용.</summary>
    public ConversionGraph Graph => _graph;

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
        // 그래프 reachability(transitive closure)로 멀티홉 변환까지 노출한다 (예: json→xlsx, svg→jpg).
        // 동일 포맷(self-edge: txt→txt AI, png→png 압축)은 '다른 형식으로 변환' 목록에서 제외.
        var input = ConversionPair.Normalize(inputExtension);
        return _graph.ReachableOutputs(input, maxHops: 3, allowLossy: true)
            .Where(o => !string.Equals(o, input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>1홉 직접 출력만 (그래프 reachability 이전의 좁은 목록이 필요한 곳용).</summary>
    public IReadOnlyList<string> DirectOutputsForInput(string inputExtension)
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
