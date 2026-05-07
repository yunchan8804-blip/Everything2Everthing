using Everything2Everything.Core;
using Everything2Everything.Core.Providers;
using Microsoft.Win32;

namespace Everything2Everything.App.Shell;

internal static class ContextMenuRegistrar
{
    private const string MainVerb = "Everything2Everything";
    private const string MainLabel = "Everything2Everything으로 변환";

    private const string SubmenuKeyPrefix = "Everything2Everything.SubMenu.";

    private static readonly (string Ext, string Label, string SortPrefix)[] PopularOutputs =
    {
        (".jpg",  "JPEG (.jpg)",     "01"),
        (".png",  "PNG (.png)",      "02"),
        (".webp", "WebP (.webp)",    "03"),
        (".pdf",  "PDF (.pdf)",      "04"),
        (".docx", "Word (.docx)",    "05"),
        (".html", "HTML (.html)",    "06"),
        (".md",   "Markdown (.md)",  "07"),
        (".txt",  "텍스트 (.txt)",   "08"),
        (".avif", "AVIF (.avif)",    "09"),
        (".gif",  "GIF (.gif)",      "10"),
        (".tif",  "TIFF (.tif)",     "11"),
        (".bmp",  "BMP (.bmp)",      "12"),
    };

    public static void Register(ConversionEngine engine)
    {
        var exe = GetAppExecutablePath();
        var icon = exe + ",0";

        foreach (var ext in CollectInputExtensions(engine))
        {
            var outputs = engine.Providers.OutputsForInput(ext)
                .Select(o => o.ToLowerInvariant())
                .ToHashSet();

            var availableOutputs = PopularOutputs
                .Where(p => outputs.Contains(p.Ext) && !string.Equals(p.Ext, ext, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (availableOutputs.Count == 0) continue;

            WriteCascade(ext, exe, icon, availableOutputs);
        }

        NotifyShell();
    }

    public static void Unregister(ConversionEngine engine)
    {
        foreach (var ext in CollectInputExtensions(engine))
        {
            DeleteVerb(ext, MainVerb);
            DeleteVerb(ext, "Everything2Everything.Quick");
            DeleteVerb(ext, "Everything2Everything.Dialog");
            DeleteSubmenuTree(ext);
        }
        NotifyShell();
    }

    private static IEnumerable<string> CollectInputExtensions(ConversionEngine engine)
    {
        return engine.Providers.Implemented
            .Where(p => p.Capability.CanRegisterContextMenu)
            .SelectMany(p => p.Capability.InputExtensions)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Select(e => e.ToLowerInvariant())
            .Distinct();
    }

    private static void WriteCascade(
        string ext,
        string exe,
        string icon,
        IReadOnlyList<(string Ext, string Label, string SortPrefix)> availableOutputs)
    {
        var submenuKeyName = SubmenuKeyPrefix + ext.TrimStart('.');

        var verbPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{MainVerb}";
        using (var verbKey = Registry.CurrentUser.CreateSubKey(verbPath, writable: true)
            ?? throw new InvalidOperationException($"레지스트리 키 생성 실패: {verbPath}"))
        {
            verbKey.SetValue(null, MainLabel, RegistryValueKind.String);
            verbKey.SetValue("MUIVerb", MainLabel, RegistryValueKind.String);
            verbKey.SetValue("Icon", icon, RegistryValueKind.String);
            verbKey.SetValue("SubCommands", "", RegistryValueKind.String);
            verbKey.SetValue("ExtendedSubCommandsKey", submenuKeyName, RegistryValueKind.String);
            try { verbKey.DeleteSubKeyTree("command", throwOnMissingSubKey: false); } catch { }
        }

        var submenuShellPath = $@"Software\Classes\{submenuKeyName}\shell";
        using (var existing = Registry.CurrentUser.OpenSubKey(submenuShellPath, writable: true))
        {
            if (existing is not null)
            {
                foreach (var name in existing.GetSubKeyNames())
                {
                    try { existing.DeleteSubKeyTree(name, throwOnMissingSubKey: false); } catch { }
                }
            }
        }

        foreach (var (outExt, outLabel, sortPrefix) in availableOutputs)
        {
            var subVerbName = $"{sortPrefix}_{outExt.TrimStart('.')}";
            var cliExt = outExt.TrimStart('.');
            WriteSubmenuItem(submenuKeyName, subVerbName, outLabel, icon,
                $"\"{exe}\" to {cliExt} \"%1\"");
        }

        WriteSubmenuItem(submenuKeyName, "98_dialog", "변환…  (옵션 선택)", icon,
            $"\"{exe}\" dialog \"%1\"");
    }

    private static void WriteSubmenuItem(string submenuKeyName, string verbName, string label, string icon, string command)
    {
        var path = $@"Software\Classes\{submenuKeyName}\shell\{verbName}";
        using var key = Registry.CurrentUser.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"서브메뉴 키 생성 실패: {path}");

        key.SetValue(null, label, RegistryValueKind.String);
        key.SetValue("MUIVerb", label, RegistryValueKind.String);
        key.SetValue("Icon", icon, RegistryValueKind.String);

        using var commandKey = key.CreateSubKey("command", writable: true)
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

    private static void DeleteSubmenuTree(string ext)
    {
        var submenuKeyName = SubmenuKeyPrefix + ext.TrimStart('.');
        var path = $@"Software\Classes\{submenuKeyName}";
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
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
            + Path.DirectorySeparatorChar + "Everything2Everything.exe";
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
