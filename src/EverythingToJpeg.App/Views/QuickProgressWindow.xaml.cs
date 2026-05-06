using System.Windows;
using EverythingToJpeg.Core;
using Wpf.Ui.Controls;

namespace EverythingToJpeg.App.Views;

public partial class QuickProgressWindow : FluentWindow
{
    private readonly int _total;
    private string? _firstSuccessOutput;

    public QuickProgressWindow(int total)
    {
        _total = total;
        InitializeComponent();
        StatusText.Text = $"0 / {_total}";
    }

    public void Report(ConvertProgress p)
    {
        if (!CheckAccess()) { Dispatcher.Invoke(() => Report(p)); return; }
        var overall = _total == 0 ? 0 : (p.Index + p.FileProgress) / _total;
        OverallProgress.Value = Math.Clamp(overall, 0, 1);
        StatusText.Text = $"{Math.Min(p.Index + 1, _total)} / {_total} — {Path.GetFileName(p.CurrentPath)}";
    }

    public void Finish(IReadOnlyList<ConvertResult> results)
    {
        if (!CheckAccess()) { Dispatcher.Invoke(() => Finish(results)); return; }

        var success = results.Count(r => r.Status == ConvertStatus.Success);
        var skipped = results.Count(r => r.Status == ConvertStatus.Skipped);
        var failed = results.Count(r => r.Status == ConvertStatus.Failed);
        var outputs = results.Sum(r => r.OutputPaths.Count);

        OverallProgress.Value = 1;
        StatusText.Text = $"성공 {success}개 (출력 {outputs}), 건너뜀 {skipped}, 실패 {failed}";
        CloseButton.IsEnabled = true;

        _firstSuccessOutput = results
            .FirstOrDefault(r => r.Status == ConvertStatus.Success)?
            .OutputPaths.FirstOrDefault();
        OpenFolderButton.IsEnabled = _firstSuccessOutput is not null;

        if (failed > 0)
        {
            var detail = string.Join("\n",
                results.Where(r => r.Status == ConvertStatus.Failed)
                    .Take(5)
                    .Select(r => $"• {Path.GetFileName(r.SourcePath)}: {r.Message}"));
            MessageBox.Show(this, "일부 파일 변환에 실패했습니다.\n\n" + detail,
                "EverythingToJpeg", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (failed == 0 && skipped == 0 && _firstSuccessOutput is not null)
        {
            OpenInExplorer(_firstSuccessOutput);
            Close();
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (_firstSuccessOutput is not null) OpenInExplorer(_firstSuccessOutput);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static void OpenInExplorer(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
