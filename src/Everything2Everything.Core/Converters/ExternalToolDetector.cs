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

        // winget(Gyan.FFmpeg)은 zip 해제본을 %LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg*\ffmpeg-*\bin에 둔다.
        // PATH에 없으므로 설치 후에도 감지되도록 패키지 폴더를 직접 스캔한다.
        var winGetPackages = Path.Combine(local, "Microsoft", "WinGet", "Packages");
        try
        {
            if (Directory.Exists(winGetPackages))
            {
                foreach (var pkgDir in Directory.EnumerateDirectories(winGetPackages, "Gyan.FFmpeg*"))
                {
                    foreach (var bin in Directory.EnumerateDirectories(pkgDir, "bin", SearchOption.AllDirectories))
                        candidates.Add(bin);
                }
            }
        }
        catch { /* WinGet 패키지 디렉터리 스캔 실패 무시 */ }

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
    /// Pandoc 바이너리를 찾는다. (1) %LOCALAPPDATA%\Pandoc (winget 기본 설치 위치),
    /// (2) WinGet Links, (3) 시스템 PATH 순.
    /// </summary>
    public static bool TryFindPandoc(out string pandocPath)
    {
        pandocPath = "";
        var candidates = new List<string>();

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
        {
            candidates.Add(Path.Combine(local, "Pandoc", "pandoc.exe"));
            candidates.Add(Path.Combine(local, "Microsoft", "WinGet", "Links", "pandoc.exe"));
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(dir))
                candidates.Add(Path.Combine(dir.Trim(), "pandoc.exe"));

        foreach (var p in candidates.Distinct())
        {
            try
            {
                if (File.Exists(p)) { pandocPath = p; return true; }
            }
            catch { /* 잘못된 경로 무시 */ }
        }
        return false;
    }

    /// <summary>
    /// ImageMagick(magick.exe) 바이너리를 찾는다. (1) %ProgramFiles%/ProgramFiles(x86) 아래
    /// ImageMagick-* 폴더, (2) 실제 PATH 순.
    /// </summary>
    public static bool TryFindMagick(out string magickPath)
    {
        magickPath = "";
        var candidates = new List<string>();

        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 })
        {
            if (string.IsNullOrEmpty(root)) continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "ImageMagick-*"))
                    candidates.Add(Path.Combine(dir, "magick.exe"));
            }
            catch { /* 디렉터리 열람 실패 무시 */ }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(dir))
                candidates.Add(Path.Combine(dir.Trim(), "magick.exe"));

        foreach (var p in candidates.Distinct())
        {
            try
            {
                if (File.Exists(p)) { magickPath = p; return true; }
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

    /// <summary>
    /// Antigravity CLI (agy, Google 개인 OAuth 재사용) 설치 여부. WinGet Links 및 PATH에서 agy.exe를 탐지한다.
    /// </summary>
    public static bool IsAgyAvailable(out string agyPath)
    {
        agyPath = "";
        var candidates = new List<string>();

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
            candidates.Add(Path.Combine(local, "Microsoft", "WinGet", "Links", "agy.exe"));

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var d in pathEnv.Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(d))
                candidates.Add(Path.Combine(d.Trim(), "agy.exe"));

        foreach (var p in candidates.Distinct())
        {
            try
            {
                if (File.Exists(p))
                {
                    agyPath = p;
                    return true;
                }
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

    /// <summary>
    /// 로컬 루프백 또는 지정된 LAN의 Switchboard Gateway 청취 여부를 100ms 이내에 신속히 감지한다.
    /// </summary>
    public static bool IsSwitchboardGatewayAvailable(out string endpoint, string? configuredEndpoint = null)
    {
        endpoint = string.IsNullOrWhiteSpace(configuredEndpoint) ? "http://127.0.0.1:8787" : configuredEndpoint.Trim().TrimEnd('/');
        try
        {
            var uri = new Uri(endpoint);
            using var tcp = new System.Net.Sockets.TcpClient();
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 8787;
            var result = tcp.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
            if (!success || !tcp.Connected) return false;
            tcp.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Switchboard Gateway (기본 http://127.0.0.1:8787) 헬스체크 및 에이전트 프로필을 비동기로 정밀 검증한다.
    /// </summary>
    public static async Task<(bool available, string? agentProfile, string endpoint)> CheckSwitchboardGatewayHealthAsync(
        string? endpoint = null, CancellationToken ct = default)
    {
        var ep = string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:8787" : endpoint.Trim().TrimEnd('/');
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(1500));
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(1500) };
            using var resp = await client.GetAsync($"{ep}/health", cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return (false, null, ep);
            var json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
            var profile = root.TryGetProperty("agentProfile", out var pProp) ? pProp.GetString() : null;
            return (ok, profile, ep);
        }
        catch
        {
            return (false, null, ep);
        }
    }
}

