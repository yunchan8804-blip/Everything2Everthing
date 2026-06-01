using System.Windows;
using Everything2Everything.App.Cli;
using Everything2Everything.App.Views;
using Everything2Everything.Core;

namespace Everything2Everything.App;

public partial class App : Application
{
    /// <summary>App·LlmProvider가 공유하는 설정 저장소 (키 저장 즉시 변환에 반영).</summary>
    public ISettingsStore Settings { get; } = new DpapiSettingsStore();

    public ConversionEngine Engine { get; }

    public App()
    {
        Engine = Everything2EverythingBootstrap.CreateDefault(Settings);
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
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void ShowConvertDialog(IReadOnlyList<string> files)
    {
        var window = new Views.MainWindow(files);
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
        var logPath = Path.Combine(Path.GetTempPath(), "Everything2Everything_quick.log");
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Quick start → {outputExtension}, {files.Count} file(s)");
        foreach (var f in files) log.AppendLine($"  src: {f}");

        var progress = new QuickProgressWindow(files.Count, outputExtension);
        progress.Show();

        try
        {
            var options = ConvertOptions.Quick();
            options.VideoPreferGpu = Settings.Get("video.gpu") != "false";
            var reporter = new Progress<ConvertProgress>(p => progress.Report(p));
            var results = await Engine.ConvertManyAsync(files, outputExtension, options, reporter);

            foreach (var r in results)
            {
                log.AppendLine($"  [{r.Status}] {Path.GetFileName(r.SourcePath)} → {r.OutputPaths.Count} output(s)");
                if (r.Message is { Length: > 0 }) log.AppendLine($"    msg: {r.Message}");
                if (r.Error is not null) log.AppendLine($"    err: {r.Error}");
                foreach (var o in r.OutputPaths) log.AppendLine($"    out: {o}");
            }

            progress.Finish(results);
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
