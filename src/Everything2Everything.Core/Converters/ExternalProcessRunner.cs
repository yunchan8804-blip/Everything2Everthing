using System.Diagnostics;
using System.Text;

namespace Everything2Everything.Core.Converters;

public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr, bool TimedOut)
{
    public bool Success => !TimedOut && ExitCode == 0;
}

/// <summary>
/// 외부 CLI 프로세스 실행을 일원화한다 — stdout/stderr 수집, 타임아웃, 취소 시 프로세스 트리 종료.
/// LibreOffice·FFmpeg·Ghostscript·qpdf 등 모든 외부 도구 어댑터가 공유하는 실행기.
/// </summary>
public static class ExternalProcessRunner
{
    public static async Task<ProcessRunResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in arguments) psi.ArgumentList.Add(a);
        if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!proc.Start())
            throw new InvalidOperationException($"프로세스를 시작하지 못했습니다: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } t) linked.CancelAfter(t);

        var timedOut = false;
        try
        {
            await proc.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 사용자 취소면 전파, 타임아웃이면 TimedOut으로 보고
            timedOut = !cancellationToken.IsCancellationRequested;
            try { proc.Kill(entireProcessTree: true); } catch { /* 이미 종료 */ }
            if (cancellationToken.IsCancellationRequested)
            {
                try { proc.WaitForExit(2000); } catch { }
                throw;
            }
        }

        // Kill 또는 종료 후 비동기 출력 콜백이 마저 도착하도록 잠깐 대기
        try { proc.WaitForExit(2000); } catch { }

        var exitCode = timedOut ? -1 : SafeExitCode(proc);
        return new ProcessRunResult(exitCode, stdout.ToString(), stderr.ToString(), timedOut);
    }

    private static int SafeExitCode(Process proc)
    {
        try { return proc.ExitCode; }
        catch { return -1; }
    }
}
