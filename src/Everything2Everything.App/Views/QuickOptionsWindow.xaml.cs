using System.Windows;
using Everything2Everything.App.ViewModels;
using Everything2Everything.Core;
using Wpf.Ui.Controls;

namespace Everything2Everything.App.Views;

/// <summary>
/// 빠른 변환(컨텍스트 메뉴에서 형식 선택) 시 뜨는 간단 옵션 팝업.
/// 출력 형식에 맞춘 핵심 옵션만 노출하고, '자세히 옵션…'은 풀 UI(MainWindow)로 넘어간다.
/// </summary>
public partial class QuickOptionsWindow : FluentWindow
{
    /// <summary>팝업이 바인딩하는 옵션 뷰모델. 변환 확정 시 ToConvertOptions()로 사용.</summary>
    public OptionsViewModel Options { get; } = new();

    /// <summary>사용자가 '자세히 옵션…'을 눌러 풀 UI로 넘어가길 원하는가.</summary>
    public bool OpenFullUi { get; private set; }

    public QuickOptionsWindow(string outputExtension, int fileCount, ISettingsStore settings)
    {
        Options.VideoPreferGpu = settings.Get("video.gpu") != "false";
        DataContext = Options;
        InitializeComponent();
        ConfigureForFormat(outputExtension, fileCount);
    }

    private void ConfigureForFormat(string ext, int count)
    {
        ext = ext.ToLowerInvariant();
        var label = ext.TrimStart('.').ToUpperInvariant();
        HeaderText.Text = count > 1 ? $"{label}(으)로 변환 · {count}개 파일" : $"{label}(으)로 변환";

        var isVideo = ext is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi";
        var isLossyAudio = ext is ".mp3" or ".aac" or ".m4a" or ".opus" or ".ogg";
        var isImageQuality = ext is ".jpg" or ".jpeg" or ".webp" or ".avif";

        VideoQuickPanel.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
        AudioQuickPanel.Visibility = (isVideo || isLossyAudio) ? Visibility.Visible : Visibility.Collapsed;
        ImageQualityPanel.Visibility = isImageQuality ? Visibility.Visible : Visibility.Collapsed;

        // 영상의 오디오 트랙임을 구분
        if (isVideo) AudioPanelLabel.Text = "오디오 비트레이트";
    }

    private void OnConvertClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        OpenFullUi = true;
        DialogResult = false;
        Close();
    }
}
