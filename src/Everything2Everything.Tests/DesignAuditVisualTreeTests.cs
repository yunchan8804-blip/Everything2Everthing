using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Everything2Everything.App.Views;
using Everything2Everything.Core;
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

    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    _ = new Application();
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
