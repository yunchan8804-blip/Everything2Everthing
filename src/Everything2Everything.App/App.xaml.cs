using System.Windows;
using Everything2Everything.App.Cli;
using Everything2Everything.App.Views;
using Everything2Everything.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Everything2Everything.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    /// <summary>App·LlmProvider가 공유하는 설정 저장소 (키 저장 즉시 변환에 반영). DI 컨테이너 단일 싱글턴.</summary>
    public ISettingsStore Settings { get; }

    public ConversionEngine Engine { get; }

    public App()
    {
        // 컴포지션 루트 — Core의 DI 확장(Scrutor 자동등록)으로 Provider/Registry/Engine/Settings를 구성.
        var services = new ServiceCollection();
        services.AddEverything2Everything();
        _services = services.BuildServiceProvider();
        Settings = _services.GetRequiredService<ISettingsStore>();
        Engine = _services.GetRequiredService<ConversionEngine>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        WireGlobalExceptionLogging();

        var parsed = CliRouter.Parse(e.Args);

        switch (parsed.Mode)
        {
            case CliRouter.Mode.Help:
                ConsoleHelper.WriteLine(CliRouter.HelpText());
                Environment.Exit(0);
                return;

            case CliRouter.Mode.Register:
                Environment.Exit(CliRouter.RunRegister(register: true));
                return;

            case CliRouter.Mode.Unregister:
                Environment.Exit(CliRouter.RunRegister(register: false));
                return;

            case CliRouter.Mode.Diagnose:
                ShowDiagnoseWindow();
                return;

            case CliRouter.Mode.Quick:
                if (parsed.Files.Count == 0)
                {
                    MessageBox.Show("변환할 파일이 없습니다.", "Everything2Everything",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown(1);
                    return;
                }
                await RunQuickAsync(parsed.Files, parsed.OutputExtension ?? ".jpg");
                return;

            case CliRouter.Mode.Dialog:
                ShowConvertDialog(parsed.Files);
                return;

            case CliRouter.Mode.ShowMain:
            default:
                ShowMainWindow();
                return;
        }
    }

    private void ShowMainWindow()
    {
        var window = new MainWindow(Engine, Settings);
        MainWindow = window;
        window.Show();
    }

    private void ShowConvertDialog(IReadOnlyList<string> files)
    {
        var window = new Views.MainWindow(Engine, Settings, files);
        MainWindow = window;
        window.Show();
    }

    private void ShowDiagnoseWindow()
    {
        var window = new DiagnoseWindow(Engine);
        MainWindow = window;
        window.Show();
    }

    private async Task RunQuickAsync(IReadOnlyList<string> files, string outputExtension)
    {
        // 옵션 팝업~변환 경로 전체를 명시적 종료 모드로 먼저 감싼다.
        // 핵심(크래시 방지): 기본 OnLastWindowClose면 옵션 팝업(첫·유일 창)이 닫히는 순간 "마지막 창 종료" 규칙이
        // 발동해 앱이 종료 시퀀스에 진입하고, 직후 ShutdownMode 설정/변환이 InvalidOperationException으로 크래시한다.
        // (또한 진행 창을 닫아도 변환을 취소한 '뒤' 명시 종료해 ffmpeg 고아 프로세스를 막는다.)
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        // 빠른 변환 전, 출력 형식에 맞춘 간단 옵션 팝업.
        // '자세히 옵션…'이면 풀 UI(MainWindow)로 전환, '취소'면 종료, '변환'이면 선택 옵션으로 진행.
        var optWin = new QuickOptionsWindow(outputExtension, files.Count, Settings);
        var confirmed = optWin.ShowDialog();
        if (optWin.OpenFullUi)
        {
            // 풀 UI로 전환 — 종료 책임을 메인 창 수명(OnLastWindowClose)에 되돌린다.
            ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;
            ShowConvertDialog(files);
            return;
        }
        if (confirmed != true) { Shutdown(0); return; }

        var logPath = Path.Combine(Path.GetTempPath(), "Everything2Everything_quick.log");
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Quick start → {outputExtension}, {files.Count} file(s)");
        foreach (var f in files) log.AppendLine($"  src: {f}");

        using var cts = new CancellationTokenSource();
        var progress = new QuickProgressWindow(files.Count, cts, outputExtension);
        var closed = new TaskCompletionSource();
        progress.Closed += (_, _) => closed.TrySetResult();
        progress.Show();

        try
        {
            var options = optWin.Options.ToConvertOptions();
            var reporter = new Progress<ConvertProgress>(p => progress.Report(p));
            var results = await Engine.ConvertManyAsync(
                files, outputExtension, options, reporter, BatchMode.Independent, cts.Token);

            foreach (var r in results)
            {
                log.AppendLine($"  [{r.Status}] {Path.GetFileName(r.SourcePath)} → {r.OutputPaths.Count} output(s)");
                if (r.Message is { Length: > 0 }) log.AppendLine($"    msg: {r.Message}");
                if (r.Error is not null) log.AppendLine($"    err: {r.Error}");
                foreach (var o in r.OutputPaths) log.AppendLine($"    out: {o}");
            }

            progress.Finish(results);
            await closed.Task; // 결과 창을 사용자가 닫을 때까지 대기(성공 경로)
        }
        catch (OperationCanceledException)
        {
            // 사용자가 취소(취소 버튼/창 닫기) — ffmpeg는 이미 중단된 뒤 여기에 도달. 진행 창 닫고 종료.
            log.AppendLine("  CANCELLED by user");
            try { progress.Close(); } catch { }
        }
        catch (Exception ex)
        {
            log.AppendLine($"  EXCEPTION {ex.GetType().Name}: {ex.Message}");
            log.AppendLine(ex.ToString());
            try { progress.Close(); } catch { }
            MessageBox.Show($"변환 중 오류: {ex.Message}\n\n로그: {logPath}", "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            try { File.WriteAllText(logPath, log.ToString()); } catch { }
            Shutdown(0); // 모든 경로에서 명시적 종료(백그라운드 잔류·고아 프로세스 방지)
        }
    }

    private static void WireGlobalExceptionLogging()
    {
        var path = Path.Combine(Path.GetTempPath(), "Everything2Everything_unhandled.log");

        void Append(string source, Exception? ex)
        {
            try
            {
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}\n{ex}\n\n");
            }
            catch { }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Append("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        Current.DispatcherUnhandledException += (_, e) =>
        {
            Append("Application.DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                "예기치 못한 오류:\n\n" + e.Exception.Message + "\n\n로그: " + path,
                "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Append("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }
}
