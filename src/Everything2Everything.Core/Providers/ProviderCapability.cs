namespace Everything2Everything.Core.Providers;

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
    IReadOnlyList<ConversionPair> SupportedConversions,
    ProviderStatus Status,
    string Summary,
    IReadOnlyList<ExternalDependency> ExternalDependencies,
    string? RoadmapNote = null)
{
    public bool CanRegisterContextMenu => Status is ProviderStatus.Available or ProviderStatus.Preview or ProviderStatus.RequiresExternal;
    public bool IsImplemented => Status is not ProviderStatus.ComingSoon and not ProviderStatus.Disabled;

    public IReadOnlyList<string> InputExtensions
        => SupportedConversions
            .Select(p => p.InputExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<string> OutputExtensions
        => SupportedConversions
            .Select(p => p.OutputExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool Supports(string inputExtension, string outputExtension)
    {
        var input = ConversionPair.Normalize(inputExtension);
        var output = ConversionPair.Normalize(outputExtension);
        return SupportedConversions.Any(p =>
            string.Equals(p.InputExtension, input, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.OutputExtension, output, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> OutputsFor(string inputExtension)
    {
        var input = ConversionPair.Normalize(inputExtension);
        return SupportedConversions
            .Where(p => string.Equals(p.InputExtension, input, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.OutputExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ConversionPair> PairsFromMatrix(IEnumerable<string> inputs, IEnumerable<string> outputs)
    {
        var inputList = inputs.Select(ConversionPair.Normalize).ToList();
        var outputList = outputs.Select(ConversionPair.Normalize).ToList();
        var pairs = new List<ConversionPair>(inputList.Count * outputList.Count);
        foreach (var i in inputList)
            foreach (var o in outputList)
                pairs.Add(new ConversionPair(i, o));
        return pairs;
    }

    public static IReadOnlyList<ConversionPair> PairsToSingleOutput(IEnumerable<string> inputs, string output)
        => inputs.Select(i => ConversionPair.Of(i, output)).ToList();
}
