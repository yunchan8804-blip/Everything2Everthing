using Microsoft.Win32;

namespace EverythingToJpeg.Core.Converters;

internal static class ExternalToolDetector
{
    public static bool TryFindLibreOfficeSoffice(out string sofficePath)
    {
        sofficePath = "";
        var candidates = new List<string>();

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        foreach (var root in new[] { pf, pfx86 })
        {
            if (string.IsNullOrEmpty(root)) continue;
            candidates.Add(Path.Combine(root, "LibreOffice", "program", "soffice.com"));
            candidates.Add(Path.Combine(root, "LibreOffice", "program", "soffice.exe"));
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\LibreOffice\UNO\InstallPath");
            if (key?.GetValue(null) is string installPath)
            {
                candidates.Add(Path.Combine(installPath, "soffice.com"));
                candidates.Add(Path.Combine(installPath, "soffice.exe"));
            }
        }
        catch { }

        foreach (var path in candidates.Distinct())
        {
            if (File.Exists(path)) { sofficePath = path; return true; }
        }
        return false;
    }

    public static bool IsWordComAvailable()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey("Word.Application");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
