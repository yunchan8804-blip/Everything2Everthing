using System;
using System.IO;
using Everything2Everything.Core.Filters;

namespace Everything2Everything.Core.Inspector;

public sealed record FileInspectorInfo(
    string FileName,
    string FullPath,
    FilterCategory Category,
    long FileSizeBytes,
    string FormattedSize,
    string Extension,
    string DimensionsOrMeta,
    bool CanOpen,
    bool CanReveal);

public static class FileInspectorBuilder
{
    public static FileInspectorInfo Build(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) fileName = filePath;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var category = QueueFilterMatcher.GetCategory(filePath);

        long size = 0;
        bool exists = false;
        try
        {
            exists = File.Exists(filePath);
            if (exists)
            {
                size = new FileInfo(filePath).Length;
            }
        }
        catch { }

        var formattedSize = HumanizeBytes(size);
        var meta = $"{ext.TrimStart('.').ToUpperInvariant()} · {category}";

        return new FileInspectorInfo(
            FileName: fileName,
            FullPath: filePath,
            Category: category,
            FileSizeBytes: size,
            FormattedSize: formattedSize,
            Extension: ext,
            DimensionsOrMeta: meta,
            CanOpen: exists,
            CanReveal: exists);
    }

    private static string HumanizeBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double b = bytes;
        int u = 0;
        while (b >= 1024.0 && u < units.Length - 1)
        {
            b /= 1024.0;
            u++;
        }
        return u == 0 ? $"{b:0} B" : $"{b:0.1} {units[u]}";
    }
}
