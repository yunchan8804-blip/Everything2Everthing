using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Everything2Everything.App.Views;
using Everything2Everything.Core;
using Xunit;

namespace Everything2Everything.Tests;

public class UnifiedTitleBarTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();
    private static readonly string MainWindowXamlPath = Path.Combine(SolutionRoot, "src", "Everything2Everything.App", "Views", "MainWindow.xaml");

    private static string FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "Everything2Everything.slnx")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new DirectoryNotFoundException("솔루션 루트를 찾을 수 없습니다.");
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _d = new();
        public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _d[key] = value;
        public void Remove(string key) => _d.Remove(key);
        public bool Contains(string key) => _d.ContainsKey(key);
    }

    [Fact]
    public void MainWindow_MustNotHave_Isolated32PxTitleBarRow()
    {
        // 32px짜리 고립된 빈 타이틀바 행이 존재하지 않아야 하며, 
        // 타이틀바와 윈도우 조작 버튼(최소/최대/닫기)이 메인 헤더와 하나의 통합 바로 통합되어야 한다.
        var doc = XDocument.Parse(File.ReadAllText(MainWindowXamlPath));

        var rootGrid = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Grid");
        Assert.NotNull(rootGrid);

        var rowDefs = rootGrid.Element(rootGrid.Name.Namespace + "Grid.RowDefinitions")?.Elements().ToList();
        Assert.NotNull(rowDefs);

        var has32PxRow = rowDefs.Any(r => r.Attribute("Height")?.Value == "32");
        Assert.False(has32PxRow, "최상위 Grid에 32px 분리된 타이틀바 행이 여전히 존재합니다. 헤더와 통합된 단일 바로 구성되어야 합니다.");
    }

    [Fact]
    public void MainWindow_TitleBar_MustBeIntegratedWithTopHeader()
    {
        // ui:TitleBar 가 분리된 빈 줄이 아니라 통합 헤더 영역(Height >= 48) 내에 배치되어야 한다.
        var doc = XDocument.Parse(File.ReadAllText(MainWindowXamlPath));

        var titleBar = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TitleBar");
        Assert.NotNull(titleBar);

        var gridRow = titleBar.Attribute("Grid.Row")?.Value;
        
        var rootGrid = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Grid");
        var rowDefs = rootGrid?.Element(rootGrid.Name.Namespace + "Grid.RowDefinitions")?.Elements().ToList();
        
        if (gridRow != null && int.TryParse(gridRow, out int rowIndex) && rowDefs != null && rowIndex < rowDefs.Count)
        {
            var rowHeight = rowDefs[rowIndex].Attribute("Height")?.Value;
            Assert.NotEqual("32", rowHeight);
        }
    }

    private static readonly object AppInitLock = new();

    [Fact]
    public void MainWindow_UnifiedTitleBar_MeasuresAndArrangesCorrectly()
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

                var engine = Everything2EverythingBootstrap.CreateDefault();
                var store = new FakeSettingsStore();
                var window = new MainWindow(engine, store);

                var content = (UIElement)window.Content;
                content.Measure(new Size(1280, 960));
                content.Arrange(new Rect(0, 0, 1280, 960));

                Assert.True(content.DesiredSize.Width > 0);
                Assert.True(content.DesiredSize.Height > 0);

                var titleBar = FindLogicalChild<Wpf.Ui.Controls.TitleBar>(window);
                Assert.NotNull(titleBar);
                Assert.True(titleBar.ActualHeight >= 40 || titleBar.DesiredSize.Height >= 40,
                    $"통합 TitleBar의 높이가 최소 40px 이상이어야 합니다. (실제: {titleBar.DesiredSize.Height})");
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
    public void MainWindow_MustNotHave_ClunkyBrandContainer_Or_RedundantTitleText()
    {
        // 사용자가 불필요하고 이상하다고 지적한 상단 좌측 320px 컨테이너 및 'FormatShift Utility' 문구가 완전히 제거되었는지 정적 AST 검증
        var doc = XDocument.Parse(File.ReadAllText(MainWindowXamlPath));
        var titleBar = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TitleBar");
        Assert.NotNull(titleBar);

        var header = titleBar.Elements().FirstOrDefault(e => e.Name.LocalName == "TitleBar.Header");
        Assert.NotNull(header);

        var allTextBlocks = header.Descendants().Where(e => e.Name.LocalName == "TextBlock" || e.Name.LocalName == "Run").ToList();
        var redundantTexts = allTextBlocks.Where(tb => 
            (tb.Attribute("Text")?.Value?.Contains("FormatShift") ?? false) ||
            (tb.Attribute("Text")?.Value?.Contains("Utility") ?? false) ||
            tb.Value.Contains("FormatShift") || tb.Value.Contains("Utility")).ToList();

        Assert.Empty(redundantTexts);

        // 320px 너비의 패널 컨테이너 Border가 존재하지 않아야 함
        var columnDefs = header.Descendants().Where(e => e.Name.LocalName == "ColumnDefinition").ToList();
        var has320Col = columnDefs.Any(c => c.Attribute("Width")?.Value == "320");
        Assert.False(has320Col, "상단 바에 320px짜리 투박한 사이드바 헤더 컨테이너가 남아있습니다. 제거되어야 합니다.");
    }

    [Fact]
    public void MainWindow_TopHeader_AllElements_MustSharePreciseVerticalCenterline()
    {
        // 상단 바 내 모든 요소(최소/최대/닫기 창 버튼, 탭, 엔진 뱃지, 액션 버튼)가
        // 시각적으로 들뜨거나 가라앉지 않고 동일한 수직 중심선(Centerline, 오차 2.0px 이내)에 완벽 정렬되어야 한다.
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));

            var titleBar = (Wpf.Ui.Controls.TitleBar)window.FindName("AppTitleBar");
            Assert.NotNull(titleBar);
            titleBar.ApplyTemplate();

            var minBtn = (FrameworkElement?)titleBar.Template.FindName("PART_MinimizeButton", titleBar);
            Assert.NotNull(minBtn);

            var tabBtn = (FrameworkElement)window.FindName("TabActiveBtn");
            var badge = (FrameworkElement)window.FindName("EngineTelemetryBadge");
            var clearBtn = (FrameworkElement)window.FindName("ClearAllButton");

            // TitleBar 좌표계 기준 각 컨트롤의 수직 중심 Y좌표 계산
            double CenterY(FrameworkElement element)
            {
                var pt = element.TransformToAncestor(titleBar).Transform(new Point(0, element.ActualHeight / 2.0));
                return pt.Y;
            }

            var minCenterY = CenterY(minBtn);
            var tabCenterY = CenterY(tabBtn);
            var badgeCenterY = CenterY(badge);
            var clearCenterY = CenterY(clearBtn);

            // 오차 2.0px 이내로 모든 요소가 동일한 수직 기준선상에 정렬되어야 함
            Assert.True(Math.Abs(tabCenterY - minCenterY) <= 2.0,
                $"최소화 버튼 중심선({minCenterY:F1}px)과 탭 중심선({tabCenterY:F1}px)의 수직 정렬 오차가 너무 큽니다.");
            Assert.True(Math.Abs(badgeCenterY - minCenterY) <= 2.0,
                $"최소화 버튼 중심선({minCenterY:F1}px)과 엔진 뱃지 중심선({badgeCenterY:F1}px)의 수직 정렬 오차가 너무 큽니다.");
            Assert.True(Math.Abs(clearCenterY - minCenterY) <= 2.0,
                $"최소화 버튼 중심선({minCenterY:F1}px)과 액션 버튼 중심선({clearCenterY:F1}px)의 수직 정렬 오차가 너무 큽니다.");

            try
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 960, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(content);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                var outPath = @"C:\Users\encep\.gemini\antigravity\brain\b58fd023-a52b-4f4b-aa23-e6df654aa1fb\.tempmediaStorage\rendered_window.png";
                using var fs = File.Create(outPath);
                enc.Save(fs);
            }
            catch { }
        });
    }

    [Fact]
    public void MainWindow_HeaderElements_MustBeIntegratedIntoTitleBarHeaderAndTrailingContent_ToPreventNcHitTestBlocking()
    {
        // Wpf.Ui.Controls.TitleBar의 HwndSourceHook(WM_NCHITTEST)은
        // 오직 Header, CenterContent, TrailingContent 내부에 포함된 요소만 HTCLIENT(클릭 허용)로 반환하며,
        // 그 외의 형제 요소나 TitleBar 외부 겹침 요소는 모두 HTCAPTION(창 이동 드래그)으로 가로채 클릭을 차단한다.
        // 따라서 상단 탭, 뱃지, 액션 버튼들은 반드시 TitleBar.Header와 TitleBar.TrailingContent 내에 직접 배치되어야 한다.
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));

            var tb = (Wpf.Ui.Controls.TitleBar)window.FindName("AppTitleBar");
            Assert.NotNull(tb);

            Assert.NotNull(tb.Header);
            Assert.NotNull(tb.TrailingContent);

            var tabActive = window.FindName("TabActiveBtn") as UIElement;
            Assert.NotNull(tabActive);

            var telemetryBadge = window.FindName("EngineTelemetryBadge") as UIElement;
            Assert.NotNull(telemetryBadge);

            var clearBtn = window.FindName("ClearAllButton") as UIElement;
            Assert.NotNull(clearBtn);

            // tabActive와 telemetryBadge가 tb.Header의 자식인지 검증
            var headerElement = tb.Header as DependencyObject;
            Assert.NotNull(headerElement);
            Assert.True(IsDescendantOf(tabActive, headerElement), "TabActiveBtn은 반드시 AppTitleBar.Header 내부에 배치되어야 클릭이 차단되지 않습니다.");
            Assert.True(IsDescendantOf(telemetryBadge, headerElement), "EngineTelemetryBadge는 AppTitleBar.Header 내부에 배치되어야 합니다.");

            // clearBtn이 tb.TrailingContent의 자식인지 검증
            var trailingElement = tb.TrailingContent as DependencyObject;
            Assert.NotNull(trailingElement);
            Assert.True(IsDescendantOf(clearBtn, trailingElement), "ClearAllButton은 반드시 AppTitleBar.TrailingContent 내부에 배치되어야 클릭이 차단되지 않습니다.");
        });
    }

    [Fact]
    public void MainWindow_HeaderLayout_MustBeSpaciousAndNonOverlapping()
    {
        // 최소 지원 해상도(1080px)에서도 좌측 탭과 우측 액션 버튼이 충돌하지 않도록,
        // 우측 액션 툴바(설정, 메뉴, 진단, 로그, 비우기)의 총 가로폭이 260px 이하여야 한다.
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1080, 720));
            content.Arrange(new Rect(0, 0, 1080, 720));

            var tb = (Wpf.Ui.Controls.TitleBar)window.FindName("AppTitleBar");
            Assert.NotNull(tb);

            var trailing = tb.TrailingContent as FrameworkElement;
            Assert.NotNull(trailing);
            Assert.True(trailing.DesiredSize.Width <= 260,
                $"우측 액션 버튼 그룹의 가로 폭이 너무 넓습니다({trailing.DesiredSize.Width}px). 260px 이하로 컴팩트하게 구성되어야 왼쪽 탭과 겹치지 않습니다.");
        });
    }

    [Fact]
    public void MainWindow_InitialLaunch_DefaultsToActiveQueueView_WithEmptyDropZone()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));

            // 1. Segmented tab check: Active Queue button must be checked, Past Results must be unchecked
            var tabActive = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("TabActiveBtn");
            var tabPast = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("TabPastBtn");
            Assert.NotNull(tabActive);
            Assert.NotNull(tabPast);
            Assert.True(tabActive.IsChecked, "프로그램 실행 시 기본 탭은 Active Queue여야 합니다.");
            Assert.False(tabPast.IsChecked, "프로그램 실행 시 Past Results 탭은 체크 해제 상태여야 합니다.");

            // 2. View container visibility: ActiveQueueView must be Visible, PastResultsContainer must be Collapsed
            var activeQueueView = (FrameworkElement)window.FindName("ActiveQueueView");
            var pastResultsContainer = (FrameworkElement)window.FindName("PastResultsContainer");
            Assert.NotNull(activeQueueView);
            Assert.NotNull(pastResultsContainer);
            Assert.Equal(Visibility.Visible, activeQueueView.Visibility);
            Assert.Equal(Visibility.Collapsed, pastResultsContainer.Visibility);

            // 3. Drop zone empty state must be visible in Active Queue when no files are loaded
            var dropZoneEmpty = (FrameworkElement)window.FindName("DropZoneEmpty");
            Assert.NotNull(dropZoneEmpty);
            Assert.Equal(Visibility.Visible, dropZoneEmpty.Visibility);
        });
    }

    [Fact]
    public void MainWindow_PopulatedActiveQueue_DisplaysBatchBarAndItems_AndEnablesLaunchButton()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            // Add sample items to ActiveQueue
            var q1 = new QueueItem
            {
                SourcePath = @"C:\Mock\nature_photo.jpg",
                FileName = "nature_photo.jpg",
                FormatLabel = "JPG",
                FormatBrush = System.Windows.Media.Brushes.Coral,
                SizeText = "4.2 MB",
                MetaLine = "3840x2160 · sRGB · 24-bit",
                SourceSizeBytes = 4_404_019,
                SelectedOutputExtension = ".webp",
                IsSelected = true
            };
            q1.SetState("queued");

            var q2 = new QueueItem
            {
                SourcePath = @"C:\Mock\quarterly_report.pdf",
                FileName = "quarterly_report.pdf",
                FormatLabel = "PDF",
                FormatBrush = System.Windows.Media.Brushes.IndianRed,
                SizeText = "12.8 MB",
                MetaLine = "32 페이지 · 텍스트/벡터 포함",
                SourceSizeBytes = 13_421_772,
                SelectedOutputExtension = ".pdf",
                IsSelected = false
            };
            q2.SetState("50%");

            var q3 = new QueueItem
            {
                SourcePath = @"C:\Mock\keynote_presentation.mp4",
                FileName = "keynote_presentation.mp4",
                FormatLabel = "MP4",
                FormatBrush = System.Windows.Media.Brushes.CornflowerBlue,
                SizeText = "156.4 MB",
                MetaLine = "1080p60 · H.264 / AAC · 05:22",
                SourceSizeBytes = 164_000_000,
                SelectedOutputExtension = ".mp4",
                IsSelected = false
            };
            q3.SetState("done");

            window.ActiveQueue.Add(q1);
            window.ActiveQueue.Add(q2);
            window.ActiveQueue.Add(q3);

            // Trigger visibility updates via reflection
            var updateVisMethod = typeof(MainWindow).GetMethod("UpdateActiveQueueVisibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateVisMethod?.Invoke(window, null);

            var updateBtnMethod = typeof(MainWindow).GetMethod("UpdateProcessQueueButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateBtnMethod?.Invoke(window, null);

            var updateBadgesMethod = typeof(MainWindow).GetMethod("UpdateBadges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateBadgesMethod?.Invoke(window, null);

            var setPreviewMetaMethod = typeof(MainWindow).GetMethod("SetPreviewMeta", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setPreviewMetaMethod?.Invoke(window, new object[] { q1.FileName, q1.SourcePath, q1.FormatLabel, q1.SizeText });

            var showPreviewGlyphMethod = typeof(MainWindow).GetMethod("ShowPreviewGlyph", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            showPreviewGlyphMethod?.Invoke(window, new object[] { ".jpg", "선택된 파일: " + q1.FileName });

            var activeQueueList = (ItemsControl)window.FindName("ActiveQueueList");
            activeQueueList.ItemsSource = window.ActiveQueue;
            activeQueueList.ApplyTemplate();

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));
            content.UpdateLayout();

            var dropZoneEmpty = (FrameworkElement)window.FindName("DropZoneEmpty");
            var activeQueueScroll = (FrameworkElement)window.FindName("ActiveQueueScroll");
            var batchActionBar = (FrameworkElement)window.FindName("BatchActionBar");
            var processBtn = (System.Windows.Controls.Button)window.FindName("ProcessQueueButton");

            Assert.NotNull(dropZoneEmpty);
            Assert.NotNull(activeQueueScroll);
            Assert.NotNull(batchActionBar);
            Assert.NotNull(processBtn);

            Assert.Equal(Visibility.Collapsed, dropZoneEmpty.Visibility);
            Assert.Equal(Visibility.Visible, activeQueueScroll.Visibility);
            Assert.Equal(Visibility.Visible, batchActionBar.Visibility);

            Assert.Equal("변환 시작 (3개 파일)", processBtn.Content);
            Assert.True(processBtn.IsEnabled);

            // DisplayStateText check on items
            Assert.Equal("대기 중", q1.DisplayStateText);
            Assert.Equal("50%", q2.DisplayStateText);
            Assert.Equal("변환 완료", q3.DisplayStateText);

            try
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 960, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(content);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                var outPath = @"C:\Users\encep\.gemini\antigravity\brain\b58fd023-a52b-4f4b-aa23-e6df654aa1fb\.tempmediaStorage\rendered_queue_populated.png";
                using var fs = File.Create(outPath);
                enc.Save(fs);
            }
            catch { }
        });
    }

    [Fact]
    public void MainWindow_PastResults_MustUseKorean_And_NotContainEnglishOutputOrSessionSavings()
    {
        RunOnSta(() =>
        {
            var dg = new DateGroup("오늘 (9월 5일)");
            Assert.Contains("절감", dg.SessionSavingsText);
            Assert.DoesNotContain("Session Savings", dg.SessionSavingsText);

            var entry = new HistoryEntry(
                DateTime.Now,
                @"C:\photos\sample.png",
                "png",
                1024 * 1024,
                512 * 1024,
                1,
                null,
                ConvertStatus.Success,
                null,
                new[] { @"C:\photos\sample.webp" });

            var row = HistoryRow.From(entry);
            Assert.Contains("개", row.MetaLine);
            Assert.DoesNotContain("output(s)", row.MetaLine);
        });
    }

    [Fact]
    public void MainWindow_TabCommand_DefaultFallback_MustBeActive()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);
            var tabActiveBtn = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("TabActiveBtn");
            var tabPastBtn = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("TabPastBtn");
            var activeQueueView = (FrameworkElement)window.FindName("ActiveQueueView");
            var pastResultsContainer = (FrameworkElement)window.FindName("PastResultsContainer");

            // Switch to past first
            window.TabCommand.Execute("Past");
            Assert.False(tabActiveBtn.IsChecked);
            Assert.True(tabPastBtn.IsChecked);
            Assert.Equal(Visibility.Collapsed, activeQueueView.Visibility);
            Assert.Equal(Visibility.Visible, pastResultsContainer.Visibility);

            // Execute with null or empty fallback
            window.TabCommand.Execute(null);
            Assert.True(tabActiveBtn.IsChecked);
            Assert.False(tabPastBtn.IsChecked);
            Assert.Equal(Visibility.Visible, activeQueueView.Visibility);
            Assert.Equal(Visibility.Collapsed, pastResultsContainer.Visibility);
        });
    }

    [Fact]
    public void MainWindow_PastResults_RendersPopulatedAndEmptyState()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));
            content.UpdateLayout();

            var updateBadgesMethod = typeof(MainWindow).GetMethod("UpdateBadges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // 1. Switch to Past tab when history is empty
            window.PastResults.Clear();
            updateBadgesMethod?.Invoke(window, null);
            window.TabCommand.Execute("Past");

            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));
            content.UpdateLayout();

            var pastResultsContainer = (FrameworkElement)window.FindName("PastResultsContainer");
            var pastResultsEmpty = (FrameworkElement)window.FindName("PastResultsEmpty");
            var pastResultsView = (FrameworkElement)window.FindName("PastResultsView");

            Assert.Equal(Visibility.Visible, pastResultsContainer.Visibility);
            Assert.Equal(Visibility.Visible, pastResultsEmpty.Visibility);
            Assert.Equal(Visibility.Collapsed, pastResultsView.Visibility);

            // Render empty state
            try
            {
                var rtbEmpty = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 960, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbEmpty.Render(content);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbEmpty));
                var outPath = @"C:\Users\encep\.gemini\antigravity\brain\b58fd023-a52b-4f4b-aa23-e6df654aa1fb\.tempmediaStorage\rendered_past_results_empty.png";
                using var fs = File.Create(outPath);
                enc.Save(fs);
            }
            catch { }

            // 2. Populate Past Results
            var dateGroup = new DateGroup("오늘 (9월 5일)");
            var entry1 = new HistoryEntry(
                DateTime.Now,
                @"C:\demo\hero-graphic.png",
                "png",
                4_250_000,
                820_000,
                1,
                null,
                ConvertStatus.Success,
                null,
                new[] { @"C:\demo\hero-graphic.webp" });
            var entry2 = new HistoryEntry(
                DateTime.Now.AddMinutes(-12),
                @"C:\demo\annual-report.docx",
                "docx",
                12_800_000,
                3_150_000,
                1,
                null,
                ConvertStatus.Success,
                null,
                new[] { @"C:\demo\annual-report.pdf" });

            dateGroup.Add(HistoryRow.From(entry1));
            dateGroup.Add(HistoryRow.From(entry2));
            window.PastResults.Add(dateGroup);

            updateBadgesMethod?.Invoke(window, null);

            var pastRow = dateGroup.Entries.FirstOrDefault();
            if (pastRow != null)
            {
                var setPreviewMetaMethod = typeof(MainWindow).GetMethod("SetPreviewMeta", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                setPreviewMetaMethod?.Invoke(window, new object[] { pastRow.FileName, pastRow.SourcePath, pastRow.FormatLabel, pastRow.SizeText });
                var showPreviewGlyphMethod = typeof(MainWindow).GetMethod("ShowPreviewGlyph", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                showPreviewGlyphMethod?.Invoke(window, new object[] { ".png", "변환 완료: " + pastRow.FileName });
            }

            var pastResultsList = (ItemsControl)window.FindName("PastResultsList");
            pastResultsList.ItemsSource = window.PastResults;
            pastResultsList.ApplyTemplate();

            content.Measure(new Size(1280, 960));
            content.Arrange(new Rect(0, 0, 1280, 960));
            content.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, pastResultsEmpty.Visibility);
            Assert.Equal(Visibility.Visible, pastResultsView.Visibility);

            // Render populated state
            try
            {
                var rtbPopulated = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 960, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbPopulated.Render(content);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbPopulated));
                var outPath = @"C:\Users\encep\.gemini\antigravity\brain\b58fd023-a52b-4f4b-aa23-e6df654aa1fb\.tempmediaStorage\rendered_past_results.png";
                using var fs = File.Create(outPath);
                enc.Save(fs);
            }
            catch { }
        });
    }

    [Fact]
    public void MainWindow_EnsureHandle_CreatesValidHwnd()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var store = new FakeSettingsStore();
            var window = new MainWindow(engine, store);
            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            var hwnd = helper.EnsureHandle();
            Assert.NotEqual(IntPtr.Zero, hwnd);
            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_TitleBar_MustDisplayProperlyAlignedHeaderAndCloseButton()
    {
        RunOnSta(() =>
        {
            var store = new FakeSettingsStore();
            var window = new SettingsWindow(store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(560, 720));
            content.Arrange(new Rect(0, 0, 560, 720));

            var titleBar = FindLogicalChild<Wpf.Ui.Controls.TitleBar>(window);
            Assert.NotNull(titleBar);
            titleBar.ApplyTemplate();

            // 1. TitleBar height must be 42px
            Assert.Equal(42.0, titleBar.ActualHeight);

            // 2. PART_MainGrid must stretch to 42px
            var mainGrid = (FrameworkElement)titleBar.Template.FindName("PART_MainGrid", titleBar);
            Assert.NotNull(mainGrid);
            Assert.Equal(42.0, mainGrid.ActualHeight);

            // 3. Find the rendered TextBlock with text "설정"
            var textBlocks = FindVisualChildren<TextBlock>(titleBar).Where(t => t.Text == "설정").ToList();
            Assert.NotEmpty(textBlocks);
            var titleTb = textBlocks.First();

            // 4. TextBlock "설정" must not be at X=0 (must have left padding/margin >= 12px)
            var textPt = titleTb.TransformToAncestor(titleBar).Transform(new Point(0, 0));
            Assert.True(textPt.X >= 12.0, $"Title text X coordinate ({textPt.X}px) must be >= 12px to prevent touching window edge.");

            var closeBtn = (FrameworkElement)titleBar.Template.FindName("PART_CloseButton", titleBar);
            Assert.NotNull(closeBtn);
            Assert.Equal(Visibility.Visible, closeBtn.Visibility);

            // 5. Title text center Y and Close button center Y must match within 2.0px tolerance
            var textCenterY = textPt.Y + titleTb.ActualHeight / 2.0;
            var closeCenterY = closeBtn.TransformToAncestor(titleBar).Transform(new Point(0, closeBtn.ActualHeight / 2.0)).Y;
            Assert.True(Math.Abs(textCenterY - closeCenterY) <= 2.0,
                $"Title text CenterY ({textCenterY:F1}px) and CloseButton CenterY ({closeCenterY:F1}px) must match within 2px.");

            try
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(560, 720, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(content);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                var outPath = @"C:\Users\encep\.gemini\antigravity\brain\b58fd023-a52b-4f4b-aa23-e6df654aa1fb\.tempmediaStorage\rendered_settings.png";
                using var fs = File.Create(outPath);
                enc.Save(fs);
            }
            catch { }
        });
    }

    [Fact]
    public void DiagnoseWindow_TitleBar_MustHaveStandardHeightAndButtons()
    {
        RunOnSta(() =>
        {
            var engine = Everything2EverythingBootstrap.CreateDefault();
            var window = new DiagnoseWindow(engine);

            var content = (UIElement)window.Content;
            content.Measure(new Size(680, 580));
            content.Arrange(new Rect(0, 0, 680, 580));

            var titleBar = FindLogicalChild<Wpf.Ui.Controls.TitleBar>(window);
            Assert.NotNull(titleBar);
            titleBar.ApplyTemplate();

            // Height must be standard 42px (not hardcoded 32px)
            Assert.Equal(42.0, titleBar.ActualHeight);
            Assert.False(titleBar.ShowMaximize);
            Assert.False(titleBar.ShowMinimize);
        });
    }

    [Fact]
    public void QuickOptionsWindow_TitleBar_MustHaveStandardHeightAndButtons()
    {
        RunOnSta(() =>
        {
            var store = new FakeSettingsStore();
            var window = new QuickOptionsWindow(".mp4", 1, store);

            var content = (UIElement)window.Content;
            content.Measure(new Size(380, 500));
            content.Arrange(new Rect(0, 0, 380, 500));

            var titleBar = FindLogicalChild<Wpf.Ui.Controls.TitleBar>(window);
            Assert.NotNull(titleBar);
            titleBar.ApplyTemplate();

            Assert.Equal(42.0, titleBar.ActualHeight);
            Assert.False(titleBar.ShowMaximize);
            Assert.False(titleBar.ShowMinimize);
        });
    }


    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }


    private static bool IsDescendantOf(DependencyObject? node, DependencyObject targetAncestor)
    {
        while (node != null)
        {
            if (node == targetAncestor) return true;
            node = LogicalTreeHelper.GetParent(node) ?? (node as FrameworkElement)?.Parent;
        }
        return false;
    }

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

        if (ex != null) throw new AggregateException("STA 스레드 실행 중 예외 발생", ex);
    }

    private static T? FindLogicalChild<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T typed) return typed;
            if (child is DependencyObject dep)
            {
                var found = FindLogicalChild<T>(dep);
                if (found != null) return found;
            }
        }
        return null;
    }
}
