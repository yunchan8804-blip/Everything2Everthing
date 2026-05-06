namespace EverythingToJpeg.Core;

internal static class OutputPathHelper
{
    public static string ResolveOutputPath(
        string outputDirectory,
        string baseName,
        string? pageSuffix,
        NameCollision collision)
    {
        var safe = SanitizeFileName(baseName);
        var fileName = string.IsNullOrEmpty(pageSuffix) ? $"{safe}.jpg" : $"{safe}{pageSuffix}.jpg";
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
                        ? Path.Combine(outputDirectory, $"{safe} ({i}).jpg")
                        : Path.Combine(outputDirectory, $"{safe}{pageSuffix} ({i}).jpg");
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
}
