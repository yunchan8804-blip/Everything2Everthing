namespace Everything2Everything.Core;

public sealed record HistoryEntry(
    DateTime Timestamp,
    string SourcePath,
    string SourceFormat,
    long SourceSizeBytes,
    long OutputSizeBytes,
    int OutputCount,
    string? MetaLine,
    ConvertStatus Status,
    string? Message,
    IReadOnlyList<string>? OutputPaths = null)
{
    public long SavingsBytes => SourceSizeBytes - OutputSizeBytes;

    public DateOnly Date => DateOnly.FromDateTime(Timestamp);

    public string? PrimaryOutputPath => OutputPaths is { Count: > 0 } list ? list[0] : null;
}
