namespace Everything2Everything.Core.Providers;

public sealed record ConversionPair(string InputExtension, string OutputExtension)
{
    public static ConversionPair Of(string input, string output)
        => new(Normalize(input), Normalize(output));

    public static string Normalize(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            throw new ArgumentException("확장자가 비어 있습니다.", nameof(ext));
        var trimmed = ext.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
