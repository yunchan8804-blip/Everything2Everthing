using System.Collections.ObjectModel;

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
    string? Message)
{
    public long SavingsBytes => SourceSizeBytes - OutputSizeBytes;

    public DateOnly Date => DateOnly.FromDateTime(Timestamp);
}

public sealed class HistoryStore
{
    private readonly ObservableCollection<HistoryEntry> _entries = new();

    public ReadOnlyObservableCollection<HistoryEntry> Entries { get; }

    public HistoryStore()
    {
        Entries = new ReadOnlyObservableCollection<HistoryEntry>(_entries);
    }

    public void Add(HistoryEntry entry) => _entries.Insert(0, entry);

    public void Clear() => _entries.Clear();

    public int Count => _entries.Count;
}
