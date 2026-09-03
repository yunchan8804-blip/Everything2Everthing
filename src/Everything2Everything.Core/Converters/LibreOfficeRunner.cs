using System.Diagnostics;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// soffice(LibreOffice) <c>--headless --convert-to</c> 호출을 단일 지점으로 집약한다.
/// HWP/HWPX 등 모든 LibreOffice 경유 변환(HwpxProvider·DocumentProvider)이 이 헬퍼를 통과한다.
///
/// 2026 리서치·적대적 검증으로 확정된 두 결함을 한 곳에서 방어한다:
/// <list type="number">
/// <item><b>직렬화 게이트</b> — LibreOffice는 프로필당 단일 인스턴스 설계(~.lock)다. 기본 프로필을 공유한 채
///   여러 soffice를 동시에 spawn하면 둘째 이후 프로세스가 첫 인스턴스에 위임되어 조용히 실패/멈춘다
///   (freedesktop Bug 106134/82775). 정적 <see cref="SemaphoreSlim"/>으로 soffice 호출을 직렬화해 락 충돌을 0으로 만든다.
///   (이미지 등 다른 Provider의 병렬성은 이 경로를 통과하지 않으므로 영향받지 않는다.)</item>
/// <item><b>타임아웃 + 프로세스 트리 kill</b> — 특정 문서에서 soffice가 무한 hang하는 사례가 다수 보고된다.
///   타임아웃 초과 시 <see cref="Process.Kill(bool)"/>로 자식까지 종료해 배치 전체가 멈추는 사고를 회수한다.</item>
/// </list>
/// 성공은 종료코드뿐 아니라 <b>출력 파일 존재</b>로 검증한다(조용한 스킵이 잘못된 결과로 둔갑하지 않게).
/// </summary>
internal static class LibreOfficeRunner
{
    // 기본 프로필 락 충돌 방지: soffice 호출을 프로세스 전역에서 직렬화한다.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public const int DefaultTimeoutSeconds = 120;

    /// <summary>
    /// <paramref name="sourcePath"/>를 <paramref name="outputFormat"/>(예: "pdf", "html", "docx", "txt")으로
    /// 변환해 <paramref name="outDir"/>에 쓰고, 생성된 결과 파일의 전체 경로를 반환한다.
    /// 결과 파일명은 LibreOffice 규칙상 <c>{입력 베이스명}.{outputFormat}</c>이다. 실패 시 예외를 던진다.
    /// 출력 파일명 충돌을 피하려면 호출측이 변환마다 고유한 <paramref name="outDir"/>를 넘길 것.
    /// </summary>
    public static async Task<string> ConvertAsync(
        string sofficePath,
        string sourcePath,
        string outDir,
        string outputFormat,
        int timeoutSeconds,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outDir);
        var seconds = timeoutSeconds <= 0 ? DefaultTimeoutSeconds : timeoutSeconds;

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sofficePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("--headless");
            psi.ArgumentList.Add("--norestore");
            psi.ArgumentList.Add("--nofirststartwizard");
            psi.ArgumentList.Add("--convert-to");
            psi.ArgumentList.Add(outputFormat);
            psi.ArgumentList.Add("--outdir");
            psi.ArgumentList.Add(outDir);
            psi.ArgumentList.Add(sourcePath);

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("LibreOffice 프로세스를 시작하지 못했습니다.");

            // 파이프 버퍼가 가득 차 soffice가 블록되는 것을 막기 위해 두 스트림을 비동기로 비운다.
            // (프로세스가 종료/강제종료되면 스트림 EOF로 두 태스크 모두 완료된다.)
            var stdErrTask = proc.StandardError.ReadToEndAsync();
            var stdOutTask = proc.StandardOutput.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(seconds));

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* 이미 종료됨 */ }
                if (ct.IsCancellationRequested)
                    throw; // 사용자 취소 — 위로 전파
                throw new TimeoutException(
                    $"LibreOffice 변환이 {seconds}초를 초과해 중단했습니다: {Path.GetFileName(sourcePath)}");
            }

            if (proc.ExitCode != 0)
            {
                var err = await SafeReadAsync(stdErrTask).ConfigureAwait(false);
                var detail = string.IsNullOrWhiteSpace(err) ? "" : " " + err.Trim();
                throw new InvalidOperationException(
                    $"LibreOffice 변환 실패 (exit {proc.ExitCode}).{detail}");
            }

            // 출력 스트림은 정상 경로에서 굳이 쓰지 않지만, 버퍼가 비워지도록 마저 완료시킨다.
            _ = await SafeReadAsync(stdOutTask).ConfigureAwait(false);

            var produced = Path.Combine(outDir,
                Path.GetFileNameWithoutExtension(sourcePath) + "." + outputFormat);
            if (!File.Exists(produced))
                throw new FileNotFoundException(
                    "LibreOffice가 결과물을 생성하지 않았습니다. 한글 입력이면 H2Orestart 확장과 Java(JRE) 설치를 확인하세요.",
                    produced);

            return produced;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> readTask)
    {
        try { return await readTask.ConfigureAwait(false); }
        catch { return ""; }
    }
}
