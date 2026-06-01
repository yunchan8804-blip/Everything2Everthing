using Microsoft.Win32;

namespace Everything2Everything.Core.Converters;

public static class ExternalToolDetector
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

    /// <summary>
    /// FFmpeg/ffprobe 바이너리 폴더를 찾는다. (1) 앱 전용 폴더(%LOCALAPPDATA%\Everything2Everything\ffmpeg),
    /// (2) 시스템 PATH 순. ffmpeg.exe와 ffprobe.exe가 모두 있는 폴더만 유효.
    /// </summary>
    public static bool TryFindFfmpeg(out string ffmpegDirectory)
    {
        ffmpegDirectory = "";
        var candidates = new List<string>();

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
            candidates.Add(Path.Combine(local, "Everything2Everything", "ffmpeg"));

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(dir))
                candidates.Add(dir.Trim());

        foreach (var dir in candidates.Distinct())
        {
            try
            {
                if (File.Exists(Path.Combine(dir, "ffmpeg.exe")) && File.Exists(Path.Combine(dir, "ffprobe.exe")))
                {
                    ffmpegDirectory = dir;
                    return true;
                }
            }
            catch { /* 잘못된 경로 무시 */ }
        }
        return false;
    }

    /// <summary>
    /// codex CLI(OpenAI Codex, ChatGPT 구독 OAuth 재사용) 설치 여부. npm 글로벌 + PATH에서
    /// codex.cmd/codex.exe/codex.ps1을 탐지한다.
    /// </summary>
    public static bool IsCodexAvailable()
    {
        var names = new[] { "codex.cmd", "codex.exe", "codex.ps1" };
        var dirs = new List<string>();

        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appdata)) dirs.Add(Path.Combine(appdata, "npm"));

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var d in pathEnv.Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(d)) dirs.Add(d.Trim());

        foreach (var dir in dirs.Distinct())
        {
            try
            {
                foreach (var n in names)
                    if (File.Exists(Path.Combine(dir, n))) return true;
            }
            catch { /* 잘못된 경로 무시 */ }
        }
        return false;
    }

    public static bool IsH2OrestartInstalled()
    {
        try
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            };
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                var loDir = Path.Combine(root, "LibreOffice", "4", "user", "uno_packages", "cache", "uno_packages");
                if (Directory.Exists(loDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(loDir, "*H2Orestart*", SearchOption.AllDirectories))
                    {
                        if (Directory.Exists(dir)) return true;
                    }
                }
                var extDir = Path.Combine(root, "LibreOffice", "4", "user", "extensions", "bundled");
                if (Directory.Exists(extDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(extDir, "*H2O*", SearchOption.AllDirectories))
                    {
                        if (Directory.Exists(dir)) return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }
}
