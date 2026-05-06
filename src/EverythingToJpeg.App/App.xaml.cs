using System.Windows;
using EverythingToJpeg.App.Cli;
using EverythingToJpeg.App.Views;
using EverythingToJpeg.Core;

namespace EverythingToJpeg.App;

public partial class App : Application
{
    public ConversionEngine Engine { get; } = EverythingToJpegBootstrap.CreateDefault();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
                    MessageBox.Show("변환할 파일이 없습니다.", "EverythingToJpeg",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown(1);
                    return;
                }
                await RunQuickAsync(parsed.Files);
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
        var window = new ConvertWindow(Engine, files);
        MainWindow = window;
        window.Show();
    }

    private void ShowDiagnoseWindow()
    {
        var window = new DiagnoseWindow(Engine);
        MainWindow = window;
        window.Show();
    }

    private async Task RunQuickAsync(IReadOnlyList<string> files)
    {
        var progress = new QuickProgressWindow(files.Count);
        progress.Show();

        try
        {
            var options = ConvertOptions.Quick();
            var reporter = new Progress<ConvertProgress>(p => progress.Report(p));
            var results = await Engine.ConvertManyAsync(files, options, reporter);
            progress.Finish(results);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"변환 중 오류: {ex.Message}", "EverythingToJpeg",
                MessageBoxButton.OK, MessageBoxImage.Error);
            progress.Close();
            Shutdown(1);
        }
    }
}
