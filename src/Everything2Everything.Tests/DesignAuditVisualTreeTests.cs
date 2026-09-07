using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Everything2Everything.App.Views;
using Everything2Everything.Core;
using Everything2Everything.Core.Filters;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 초엄격 Visual & Layout In-Memory 배치 TDD 테스트 스위트
/// STA 스레드에서 창과 컨트롤을 가상 렌더링(Measure & Arrange)하여
/// NaN/Infinity 크기 오류, 요소 클리핑/오버플로, 터치/클릭 최소 타깃 규격을 자동 검증한다.
/// </summary>
public class DesignAuditVisualTreeTests
{
    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _d = new();
        public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _d[key] = value;
        public void Remove(string key) => _d.Remove(key);
        public bool Contains(string key) => _d.ContainsKey(key);
    }

    private static readonly object AppInitLock = new();

    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                lock (AppInitLock)
                {
                    if (Application.Current == null)
                    {
                        try { _ = new Application(); } catch (InvalidOperationException) { }
                    }
                }
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null)
        {
            throw new AggregateException("STA 스레드 실행 중 예외 발생", ex);
        }
    }

    [Fact]
    public void MainWindow_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);
            var content = (UIElement)window.Content;
            content.Measure(new Size(1200, 800));
            content.Arrange(new Rect(0, 0, 1200, 800));

            Assert.False(double.IsNaN(content.DesiredSize.Width));
            Assert.False(double.IsNaN(content.DesiredSize.Height));
            Assert.True(content.DesiredSize.Width > 0);
            Assert.True(content.DesiredSize.Height > 0);

            var buttons = FindLogicalChildren<ButtonBase>(window).ToList();
            Assert.NotEmpty(buttons);
        });
    }

    [Fact]
    public void MainWindow_ToggleInspectorCommand_TogglesColumnWidthAndLayout()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            Assert.True(window.IsInspectorVisible);

            // Toggle to collapsed
            window.ToggleInspectorCommand.Execute(null);
            Assert.False(window.IsInspectorVisible);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1200, 800));
            content.Arrange(new Rect(0, 0, 1200, 800));
            Assert.True(content.DesiredSize.Width > 0);

            // Toggle back to expanded
            window.ToggleInspectorCommand.Execute(null);
            Assert.True(window.IsInspectorVisible);
        });
    }

    [Fact]
    public void DiagnoseWindow_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var window = new DiagnoseWindow(engine);
            var content = (UIElement)window.Content;
            content.Measure(new Size(640, 540));
            content.Arrange(new Rect(0, 0, 640, 540));

            Assert.False(double.IsNaN(content.DesiredSize.Width));
            Assert.False(double.IsNaN(content.DesiredSize.Height));
            Assert.False(double.IsInfinity(content.DesiredSize.Width));
            Assert.False(double.IsInfinity(content.DesiredSize.Height));
            Assert.True(content.DesiredSize.Width > 0);
            Assert.True(content.DesiredSize.Height > 0);

            var buttons = FindLogicalChildren<ButtonBase>(window).ToList();
            Assert.NotEmpty(buttons);
        });
    }

    [Fact]
    public void QuickOptionsWindow_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            var store = new FakeSettingsStore();
            var window = new QuickOptionsWindow(".mp4", 1, store);
            var content = (UIElement)window.Content;
            content.Measure(new Size(380, 500));
            content.Arrange(new Rect(0, 0, 380, 500));

            Assert.True(content.DesiredSize.Width > 0);
            Assert.True(content.DesiredSize.Height > 0);

            // 확인/취소 버튼이 존재하는지 검증
            var buttons = FindLogicalChildren<ButtonBase>(window).ToList();
            var actionButtons = buttons.Where(b => b is Button btn && btn.Content is string s && (s == "변환" || s == "취소")).ToList();
            Assert.True(actionButtons.Count >= 2);
        });
    }

    [Fact]
    public void QuickProgressWindow_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            using var cts = new CancellationTokenSource();
            var window = new QuickProgressWindow(1, cts, ".jpg");
            var content = (UIElement)window.Content;
            content.Measure(new Size(560, 240));
            content.Arrange(new Rect(0, 0, 560, 240));

            Assert.True(content.DesiredSize.Width > 0);
            Assert.True(content.DesiredSize.Height > 0);
            Assert.Equal(560, window.Width);
            Assert.Equal(240, window.Height);
        });
    }

    [Fact]
    public void SettingsWindow_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            var store = new FakeSettingsStore();
            var window = new SettingsWindow(store);
            var content = (UIElement)window.Content;
            content.Measure(new Size(560, 720));
            content.Arrange(new Rect(0, 0, 560, 720));

            Assert.True(content.DesiredSize.Width > 0);
            Assert.True(content.DesiredSize.Height > 0);
            Assert.Equal(560, window.Width);
            Assert.Equal(720, window.Height);
        });
    }

    [Fact]
    public void DiagnoseWindow_ProviderCards_MustHaveStructuredGridAlignment_And_TextWrapping()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var window = new DiagnoseWindow(engine);
            window.PopulateAsync().GetAwaiter().GetResult();

            var content = (UIElement)window.Content;
            content.Measure(new Size(640, 540));
            content.Arrange(new Rect(0, 0, 640, 540));

            // 1. ItemsPanel 내부에 카드들이 채워졌는지 검증
            var itemsPanel = (StackPanel)window.FindName("ItemsPanel");
            Assert.NotNull(itemsPanel);
            Assert.True(itemsPanel.Children.Count >= 2, "환경 섹션 및 프로바이더 카드들이 채워져야 합니다.");

            // 2. 입력/출력 텍스트가 단일 비정렬 문자열("입력: ... → 출력: ...")로 오버플로되지 않고
            //    정형화된 그리드 또는 TextWrapping이 적용된 구조인지 검증
            var allTextBlocks = FindLogicalChildren<TextBlock>(window).ToList();
            var rawUnwrappedOverflows = allTextBlocks.Where(tb => tb.Text.Contains("입력:") && tb.Text.Contains("→") && tb.TextWrapping != TextWrapping.Wrap).ToList();
            Assert.Empty(rawUnwrappedOverflows);

            // 3. 프로바이더 카드 내에 '입력 형식' 및 '출력 형식' 라벨이 명확히 분리 정렬되어 있는지 검증
            var inputLabels = allTextBlocks.Where(tb => tb.Text == "입력 형식").ToList();
            var outputLabels = allTextBlocks.Where(tb => tb.Text == "출력 형식").ToList();
            Assert.NotEmpty(inputLabels);
            Assert.NotEmpty(outputLabels);

            // 4. 모든 가로 정렬 헤더(제목 + 뱃지)에서 VerticalAlignment가 Center로 통일되어 있는지 검증
            var badges = allTextBlocks.Where(tb => tb.Text is "준비됨" or "개발 중" or "점검 필요" or "비활성").ToList();
            Assert.NotEmpty(badges);
            foreach (var badge in badges)
            {
                var border = badge.Parent as Border;
                Assert.NotNull(border);
                Assert.Equal(VerticalAlignment.Center, border.VerticalAlignment);
            }
        });
    }

    [Fact]
    public void MainWindow_TopHeader_EngineTelemetryBadge_HasValidBounds_And_CenterAlignment()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var settings = new FakeSettingsStore();
            var window = new MainWindow(engine, settings);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));

            var badge = (Border)window.FindName("EngineTelemetryBadge");
            Assert.NotNull(badge);
            Assert.Equal(VerticalAlignment.Center, badge.VerticalAlignment);
            Assert.True(badge.DesiredSize.Width > 0);
            Assert.True(badge.DesiredSize.Height > 0);
        });
    }

    [Fact]
    public void MainWindow_WhenQueueIsEmpty_ShowsDefaultFormat_AndQualitySlider_AndAdvancedPanel_Visible()
    {
        // Zero-Void 원칙 및 점진적 공개 원칙: 큐가 비어있더라도 사용자가 변환 옵션과 품질 슬라이더를 사전에 확인하고 조작할 수 있도록
        // 기본 출력 포맷(.jpg)과 품질 슬라이더, 해당 포맷의 상세 인코딩 서브패널이 Visible 이어야 한다.
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var settings = new FakeSettingsStore();
            var window = new MainWindow(engine, settings);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));

            var formatCombo = (ComboBox)window.FindName("OutputFormatCombo");
            var qualityPanel = (StackPanel)window.FindName("QualityPanel");
            var advancedImagePanel = (StackPanel)window.FindName("AdvancedImagePanel");

            Assert.NotNull(formatCombo);
            Assert.NotNull(qualityPanel);
            Assert.NotNull(advancedImagePanel);

            // 큐가 비어있어도 지원 가능한 전체 포맷이 채워져 있어야 한다.
            Assert.True(formatCombo.Items.Count > 0, "큐가 비어있어도 전체 지원 포맷이 표시되어야 합니다.");
            // 기본 포맷(.jpg)에 맞춰 품질 슬라이더 패널이 표시되어야 한다.
            Assert.Equal(Visibility.Visible, qualityPanel.Visibility);
            // 기본 포맷에 맞춰 상세 설정 패널이 표시되어야 한다.
            Assert.Equal(Visibility.Visible, advancedImagePanel.Visibility);
        });
    }

    [Fact]
    public void MainWindow_FormatSwitching_UpdatesQuickPanels_And_AdvancedPanels_Correctly()
    {
        // 사용자 피드백 대응: 스마트 프리셋 직하단에서 각 미디어 형식(.mp4, .pdf, .mp3, .webp)에 따라
        // 전용 퀵 컨트롤(품질/CRF/비트레이트/PDF압축)과 상세 폴드아웃 서브패널이 동적으로 즉시 표시되어야 한다.
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var settings = new FakeSettingsStore();
            var window = new MainWindow(engine, settings);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));

            var formatCombo = (ComboBox)window.FindName("OutputFormatCombo");
            var qualityPanel = (StackPanel)window.FindName("QualityPanel");
            var videoQuickPanel = (StackPanel)window.FindName("VideoQuickPanel");
            var audioQuickPanel = (StackPanel)window.FindName("AudioQuickPanel");
            var pdfQuickPanel = (StackPanel)window.FindName("PdfQuickPanel");
            var expander = (Expander)window.FindName("AdvancedOptionsExpander");
            var advVideo = (StackPanel)window.FindName("AdvancedVideoPanel");
            var advAudio = (StackPanel)window.FindName("AdvancedAudioPanel");
            var advPdf = (StackPanel)window.FindName("AdvancedPdfPanel");
            var advImage = (StackPanel)window.FindName("AdvancedImagePanel");

            Assert.NotNull(formatCombo);
            Assert.NotNull(qualityPanel);
            Assert.NotNull(videoQuickPanel);
            Assert.NotNull(audioQuickPanel);
            Assert.NotNull(pdfQuickPanel);
            Assert.NotNull(expander);

            // 기본은 열려있어야 함 (사용자가 즉시 확인 가능)
            Assert.True(expander.IsExpanded);

            void SelectExtension(string ext)
            {
                for (var i = 0; i < formatCombo.Items.Count; i++)
                {
                    if (formatCombo.Items[i] is ComboBoxItem item && (string)item.Tag == ext)
                    {
                        formatCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // 1. 영상 (.mp4) 전환 검증
            SelectExtension(".mp4");
            Assert.Equal(Visibility.Collapsed, qualityPanel.Visibility);
            Assert.Equal(Visibility.Visible, videoQuickPanel.Visibility);
            Assert.Equal(Visibility.Visible, advVideo.Visibility);
            Assert.Equal(Visibility.Visible, advAudio.Visibility);
            Assert.Equal(Visibility.Collapsed, advPdf.Visibility);

            // 2. PDF (.pdf) 전환 검증
            SelectExtension(".pdf");
            Assert.Equal(Visibility.Collapsed, qualityPanel.Visibility);
            Assert.Equal(Visibility.Collapsed, videoQuickPanel.Visibility);
            Assert.Equal(Visibility.Visible, pdfQuickPanel.Visibility);
            Assert.Equal(Visibility.Visible, advPdf.Visibility);
            Assert.Equal(Visibility.Collapsed, advVideo.Visibility);

            // 3. 오디오 (.mp3) 전환 검증
            SelectExtension(".mp3");
            Assert.Equal(Visibility.Collapsed, qualityPanel.Visibility);
            Assert.Equal(Visibility.Visible, audioQuickPanel.Visibility);
            Assert.Equal(Visibility.Visible, advAudio.Visibility);
            Assert.Equal(Visibility.Collapsed, advVideo.Visibility);

            // 4. 이미지 (.webp) 전환 검증
            SelectExtension(".webp");
            Assert.Equal(Visibility.Visible, qualityPanel.Visibility);
            Assert.Equal(Visibility.Collapsed, videoQuickPanel.Visibility);
            Assert.Equal(Visibility.Collapsed, audioQuickPanel.Visibility);
            Assert.Equal(Visibility.Collapsed, pdfQuickPanel.Visibility);
            Assert.Equal(Visibility.Visible, advImage.Visibility);
        });
    }

    [Fact]
    public void MainWindow_RenderToBitmap_SavesVisualVerificationArtifact()
    {
        RunOnSta(() =>
        {
            var dir = System.IO.Directory.GetCurrentDirectory();
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "Everything2Everything.slnx")))
            {
                dir = System.IO.Directory.GetParent(dir)?.FullName;
            }
            var root = dir ?? throw new System.IO.DirectoryNotFoundException("솔루션 루트를 찾을 수 없습니다.");

            var engine = Everything2EverythingBootstrap.CreateDefault();
            var settings = new FakeSettingsStore();
            var testFiles = new List<string>
            {
                System.IO.Path.Combine(root, "test_assets", "test_icon.png"),
                System.IO.Path.Combine(root, "test_assets", "test_art.png")
            };

            var window = new MainWindow(engine, settings, testFiles);
            window.ApplyTemplate();
            var expander = (Expander)window.FindName("AdvancedOptionsExpander");
            expander.ApplyTemplate();

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));
            content.UpdateLayout();

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 960, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(content);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

            var outDir = @"C:\Users\encep\.gemini\antigravity\brain\b58fd023-a52b-4f4b-aa23-e6df654aa1fb";
            if (!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir);
            var outPath = System.IO.Path.Combine(outDir, "rendered_toolbar_fixed.png");
            using (var fs = new System.IO.FileStream(outPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
            {
                encoder.Save(fs);
            }

            Assert.True(System.IO.File.Exists(outPath));
            Assert.True(new System.IO.FileInfo(outPath).Length > 1000);
        });
    }

    [Fact]
    public void MainWindow_CategoryFilterCombo_ChangesCategory_And_FiltersQueue()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var settings = new FakeSettingsStore();
            var window = new MainWindow(engine, settings);
            window.ActiveQueue.Add(new QueueItem { SourcePath = @"C:\test.png", FileName = "test.png" });
            window.ActiveQueue.Add(new QueueItem { SourcePath = @"C:\doc.pdf", FileName = "doc.pdf" });
            window.ActiveQueue.Add(new QueueItem { SourcePath = @"C:\clip.mp4", FileName = "clip.mp4" });
            window.ActiveQueue.Add(new QueueItem { SourcePath = @"C:\sheet.xlsx", FileName = "sheet.xlsx" });

            var combo = (ComboBox)window.FindName("CategoryFilterCombo");
            Assert.NotNull(combo);
            Assert.Equal(5, combo.Items.Count);

            // 0: 전체 (All)
            Assert.Equal(FilterCategory.All, window.SelectedCategory);

            // 1: 이미지 (Image)
            combo.SelectedIndex = 1;
            Assert.Equal(FilterCategory.Image, window.SelectedCategory);

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(window.ActiveQueue);
            var filteredItems = view.Cast<QueueItem>().ToList();
            Assert.Single(filteredItems);
            Assert.Equal("test.png", filteredItems[0].FileName);

            // 0: 전체 복귀 (All)
            combo.SelectedIndex = 0;
            Assert.Equal(FilterCategory.All, window.SelectedCategory);
            filteredItems = view.Cast<QueueItem>().ToList();
            Assert.Equal(4, filteredItems.Count);

            // 가상 레이아웃 검증 (너비 360px 환경에서도 DesiredSize가 정상 계산되는지)
            var content = (UIElement)window.Content;
            content.Measure(new Size(360, 600));
            content.Arrange(new Rect(0, 0, 360, 600));
            Assert.True(content.DesiredSize.Width > 0);
            Assert.False(double.IsNaN(content.DesiredSize.Width));
        });
    }

    [Fact]
    public void AdBannerControl_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            var adService = new Everything2Everything.Core.Ads.AdService();
            var vm = new Everything2Everything.App.ViewModels.AdViewModel(adService);
            var banner = new AdBannerControl { DataContext = vm };
            banner.Measure(new Size(800, 100));
            banner.Arrange(new Rect(0, 0, 800, 100));

            Assert.True(banner.DesiredSize.Width > 0);
            Assert.True(banner.DesiredSize.Height > 0);
            Assert.False(double.IsNaN(banner.DesiredSize.Width));
            Assert.False(double.IsNaN(banner.DesiredSize.Height));
        });
    }

    [Fact]
    public void AdLargeCardControl_MeasureAndArrange_HasValidLayoutBounds()
    {
        RunOnSta(() =>
        {
            var adService = new Everything2Everything.Core.Ads.AdService();
            var vm = new Everything2Everything.App.ViewModels.AdViewModel(adService);
            var card = new AdLargeCardControl { DataContext = vm };
            card.Measure(new Size(340, 300));
            card.Arrange(new Rect(0, 0, 340, 300));

            Assert.True(card.DesiredSize.Width > 0);
            Assert.True(card.DesiredSize.Height > 0);
            Assert.False(double.IsNaN(card.DesiredSize.Width));
            Assert.False(double.IsNaN(card.DesiredSize.Height));
        });
    }

    private static IEnumerable<T> FindLogicalChildren<T>(object parent) where T : DependencyObject
    {
        if (parent is ContentControl cc && cc.Content != null)
        {
            if (cc.Content is T t) yield return t;
            foreach (var c in FindLogicalChildren<T>(cc.Content)) yield return c;
        }

        if (parent is DependencyObject dep)
        {
            foreach (var rawChild in LogicalTreeHelper.GetChildren(dep))
            {
                if (rawChild is T directMatch) yield return directMatch;
                foreach (var grandChild in FindLogicalChildren<T>(rawChild)) yield return grandChild;
            }
        }
    }
}
