using EverythingToJpeg.Core;
using EverythingToJpeg.Core.Providers;
using Microsoft.Win32;

namespace EverythingToJpeg.App.Shell;

internal static class ContextMenuRegistrar
{
    private const string QuickVerb = "EverythingToJpeg.Quick";
    private const string DialogVerb = "EverythingToJpeg.Dialog";

    private const string QuickLabel = "JPEG로 빠른 변환";
    private const string DialogLabel = "JPEG로 변환…";

    public static void Register(ConversionEngine engine)
    {
        var exe = GetAppExecutablePath();
        var icon = exe + ",0";

        foreach (var ext in CollectExtensions(engine))
        {
            WriteVerb(ext, QuickVerb, QuickLabel, icon, $"\"{exe}\" quick \"%1\"");
            WriteVerb(ext, DialogVerb, DialogLabel, icon, $"\"{exe}\" dialog \"%1\"");
        }

        NotifyShell();
    }

    public static void Unregister(ConversionEngine engine)
    {
        foreach (var ext in CollectExtensions(engine))
        {
            DeleteVerb(ext, QuickVerb);
            DeleteVerb(ext, DialogVerb);
        }
        NotifyShell();
    }

    private static IEnumerable<string> CollectExtensions(ConversionEngine engine)
    {
        return engine.Providers.Implemented
            .Where(p => p.Capability.CanRegisterContextMenu)
            .SelectMany(p => p.Capability.Extensions)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Select(e => e.ToLowerInvariant())
            .Distinct();
    }

    private static void WriteVerb(string ext, string verb, string label, string icon, string command)
    {
        var keyPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{verb}";
        using var verbKey = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
            ?? throw new InvalidOperationException($"레지스트리 키 생성 실패: {keyPath}");

        verbKey.SetValue(null, label, RegistryValueKind.String);
        verbKey.SetValue("Icon", icon, RegistryValueKind.String);
        verbKey.SetValue("MUIVerb", label, RegistryValueKind.String);

        using var commandKey = verbKey.CreateSubKey("command", writable: true)
            ?? throw new InvalidOperationException("command 하위 키 생성 실패");
        commandKey.SetValue(null, command, RegistryValueKind.String);
    }

    private static void DeleteVerb(string ext, string verb)
    {
        var parentPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
        try
        {
            using var parent = Registry.CurrentUser.OpenSubKey(parentPath, writable: true);
            parent?.DeleteSubKeyTree(verb, throwOnMissingSubKey: false);
        }
        catch
        {
        }
    }

    private static string GetAppExecutablePath()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe) && File.Exists(exe)) return exe;
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar + "EverythingToJpeg.exe";
    }

    private static void NotifyShell()
    {
        try { NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero); }
        catch { }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
