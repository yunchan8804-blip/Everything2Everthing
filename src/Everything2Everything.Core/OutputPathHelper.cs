namespace Everything2Everything.Core;

internal static class OutputPathHelper
{
    public static string ResolveOutputPath(
        string outputDirectory,
        string baseName,
        string? pageSuffix,
        string outputExtension,
        NameCollision collision)
    {
        var safe = SanitizeFileName(baseName);
        var ext = NormalizeExtension(outputExtension);
        var fileName = string.IsNullOrEmpty(pageSuffix) ? $"{safe}{ext}" : $"{safe}{pageSuffix}{ext}";
        var fullPath = Path.Combine(outputDirectory, fileName);

        if (!File.Exists(fullPath)) return fullPath;

        switch (collision)
        {
            case NameCollision.Overwrite:
                return fullPath;
            case NameCollision.Skip:
                return fullPath;
            case NameCollision.AppendNumber:
            default:
                for (var i = 1; i < 10000; i++)
                {
                    var candidate = string.IsNullOrEmpty(pageSuffix)
                        ? Path.Combine(outputDirectory, $"{safe} ({i}){ext}")
                        : Path.Combine(outputDirectory, $"{safe}{pageSuffix} ({i}){ext}");
                    if (!File.Exists(candidate)) return candidate;
                }
                return fullPath;
        }
    }

    public static bool ShouldSkip(string finalPath, NameCollision collision)
        => collision == NameCollision.Skip && File.Exists(finalPath);

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        Span<char> buffer = stackalloc char[name.Length];
        for (var i = 0; i < name.Length; i++)
        {
            buffer[i] = Array.IndexOf(invalid, name[i]) >= 0 ? '_' : name[i];
        }
        return new string(buffer);
    }

    private static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return ".jpg";
        return ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
    }
}
