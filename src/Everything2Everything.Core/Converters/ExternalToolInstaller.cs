using System.Diagnostics;
using System.Text;

namespace Everything2Everything.Core.Converters;

/// <summary>외부 명령 한 번을 실행하는 시드. 프로세스 실행을 추상화해 설치 로직을 단위 테스트 가능하게 한다.</summary>
public interface IExternalCommandRunner
{
    Task<ExternalCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct);
}

public sealed record ExternalCommandResult(int ExitCode, string Output);

/// <summary>System.Diagnostics.Process 기반 기본 구현. 숨김 실행 + 출력(꼬리) 캡처 + 취소 시 Kill.</summary>
public sealed class SystemCommandRunner : IExternalCommandRunner
{
    public async Task<ExternalCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        using var proc = new Process { StartInfo = psi };
        var outTail = new StringBuilder();
        var errTail = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendCapped(outTail, e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendCapped(errTail, e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Kill(proc);
            return new ExternalCommandResult(-1, "취소됨");
        }

        var joined = (outTail + "\n" + errTail).Trim();
        return new ExternalCommandResult(proc.ExitCode, joined);
    }

    private static void Kill(Process proc)
    {
        try { proc.Kill(entireProcessTree: true); }
        catch { }
        try { proc.WaitForExit(); } catch { }
    }

    /// <summary>출력이 폭주하지 않도록 꼬리 일부만 유지한다.</summary>
    private static void AppendCapped(StringBuilder sb, string line, int max = 4000)
    {
        lock (sb)
        {
            if (sb.Length > max * 3) return;
            sb.AppendLine(line);
            if (sb.Length > max)
            {
                var keep = sb.ToString(sb.Length - max, max);
                sb.Clear();
                sb.Append('…');
                sb.Append(keep);
            }
        }
    }
}

public sealed record ExternalToolInstallResult(bool Success, int ExitCode, string Output, string? Message);

/// <summary>
/// 외부 도구 설치 실행기. Winget 도구는 winget 무인 설치 명령을 만들고 실행한 뒤,
/// 설치 여부를 다시 감지(IsInstalled)해 실제 성공 여부를 판정한다.
/// Manual(수동) 도구는 실행하지 않고 안내 문구만 돌려준다.
/// </summary>
public sealed class ExternalToolInstaller
{
    private readonly IExternalCommandRunner _runner;

    public ExternalToolInstaller(IExternalCommandRunner? runner = null)
        => _runner = runner ?? new SystemCommandRunner();

    public static bool IsWingetAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("winget", "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ExternalToolInstallResult> InstallAsync(ExternalToolDefinition tool, CancellationToken ct)
    {
        // 수동 도구(H2Orestart 등)는 자동 설치를 시도하지 않고 안내문을 돌려준다.
        if (tool.Kind != ExternalToolInstallKind.Winget || string.IsNullOrWhiteSpace(tool.WingetId))
            return new ExternalToolInstallResult(tool.IsInstalled(), 0, "", tool.ManualNote);

        var args = $"install --id {tool.WingetId} -e --source winget --silent " +
                   "--accept-package-agreements --accept-source-agreements --disable-interactivity";

        ExternalCommandResult result;
        try
        {
            result = await _runner.RunAsync("winget", args, null, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ExternalToolInstallResult(false, -1, ex.Message, "winget 실행 실패 — 설치할 수 없습니다.");
        }

        // 성공 판정은 exit code가 아니라 '설치 후 실제 감지'로 한다(앱 입장의 실질 기준).
        var installed = tool.IsInstalled();
        return new ExternalToolInstallResult(
            installed,
            result.ExitCode,
            result.Output,
            installed ? null : $"설치 실패 또는 아직 감지 안 됨 (코드 {result.ExitCode})");
    }
}
