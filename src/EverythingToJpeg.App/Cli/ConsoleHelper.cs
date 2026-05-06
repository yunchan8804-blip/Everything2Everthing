using System.Runtime.InteropServices;

namespace EverythingToJpeg.App.Cli;

internal static class ConsoleHelper
{
    private static bool _attached;

    public static void WriteLine(string text)
    {
        EnsureAttached();
        Console.Out.WriteLine(text);
        Console.Out.Flush();
    }

    private static void EnsureAttached()
    {
        if (_attached) return;
        _attached = AttachConsole(-1);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}
