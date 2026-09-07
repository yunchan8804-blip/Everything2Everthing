using System.Threading;
using System.Windows;
using Everything2Everything.App.ViewModels;

namespace Everything2Everything.App.Views;

/// <summary>첫 실행 외부 도구 설치 마법사. 선택된(기본 전체) 도구를 순차 설치하고 상태를 갱신한다.</summary>
public partial class ToolSetupWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ToolSetupViewModel _vm;
    private CancellationTokenSource? _cts;

    public ToolSetupWindow()
    {
        InitializeComponent();
        _vm = new ToolSetupViewModel();
        DataContext = _vm;
    }

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_vm.IsInstalling) return;
        _cts = new CancellationTokenSource();
        InstallButton.IsEnabled = false;
        try
        {
            await _vm.InstallSelectedAsync(_cts.Token);
            // 온보딩 완료 → 다이얼로그를 닫아 ShowMainWindow가 뒤이어 메인 창을 띄우게 한다.
            Close();
        }
        finally
        {
            InstallButton.IsEnabled = true;
        }
    }

    private void OnLater(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
