using System.Windows;
using Everything2Everything.Core;
using Wpf.Ui.Controls;

namespace Everything2Everything.App.Views;

public partial class QuickProgressWindow : FluentWindow
{
    private readonly int _total;
    private string? _firstSuccessOutput;

    public QuickProgressWindow(int total, string? outputExtension = null)
    {
        _total = total;
        InitializeComponent();

        var label = string.IsNullOrWhiteSpace(outputExtension)
            ? "빠른 변환"
            : $"{outputExtension.TrimStart('.').ToUpperInvariant()}(으)로 변환";
        Title = label;
        WindowTitleBar.Title = label;

        StatusText.Text = $"0 / {_total}";
    }

    public void Report(ConvertProgress p)
    {
        if (!CheckAccess()) { Dispatcher.Invoke(() => Report(p)); return; }
        var overall = _total == 0 ? 0 : (p.Index + p.FileProgress) / _total;
        var clamped = Math.Clamp(overall, 0, 1);
        OverallProgress.Value = clamped;
        // 진행률이 멈춘 듯 보이지 않도록 % 표시 (영상 트랜스코딩처럼 오래 걸려도 단계 진행이 보이게)
        OverallProgress.IsIndeterminate = clamped <= 0;
        PercentText.Text = clamped <= 0 ? "처리 중…" : $"{(int)Math.Round(clamped * 100)}%";
        StatusText.Text = $"{Math.Min(p.Index + 1, _total)} / {_total} — {Path.GetFileName(p.CurrentPath)}";
    }

    public void Finish(IReadOnlyList<ConvertResult> results)
    {
        if (!CheckAccess()) { Dispatcher.Invoke(() => Finish(results)); return; }

        var success = results.Count(r => r.Status == ConvertStatus.Success);
        var skipped = results.Count(r => r.Status == ConvertStatus.Skipped);
        var failed = results.Count(r => r.Status == ConvertStatus.Failed);
        var outputs = results.Sum(r => r.OutputPaths.Count);

        OverallProgress.IsIndeterminate = false;
        OverallProgress.Value = 1;
        PercentText.Text = failed > 0 ? "완료(일부 실패)" : "100%";
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
                "Everything2Everything", MessageBoxButton.OK, MessageBoxImage.Warning);
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
