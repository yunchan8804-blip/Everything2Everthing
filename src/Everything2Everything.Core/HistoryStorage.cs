using System.Text.Json;
using System.Text.Json.Serialization;

namespace Everything2Everything.Core;

public static class HistoryStorage
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Everything2Everything");

    private static readonly string FilePath = Path.Combine(Dir, "history.jsonl");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static IReadOnlyList<HistoryEntry> Load()
    {
        if (!File.Exists(FilePath)) return Array.Empty<HistoryEntry>();

        var list = new List<HistoryEntry>();
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions);
                    if (entry is not null) list.Add(entry);
                }
                catch
                {
                    // 손상된 줄은 무시
                }
            }
        }
        catch
        {
            return Array.Empty<HistoryEntry>();
        }
        return list;
    }

    public static void Append(HistoryEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            File.AppendAllText(FilePath, json + Environment.NewLine);
        }
        catch
        {
            // 영구 저장 실패는 메모리 동작에 영향 없음
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }

    public static string LocationHint => FilePath;
}
