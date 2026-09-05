using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Everything2Everything.App.Views;
using Xunit;

namespace Everything2Everything.Tests;

public class CategoryGlyphsTests
{
    [Theory]
    [InlineData(".jpg", 80)]
    [InlineData(".mp4", 80)]
    [InlineData(".pdf", 80)]
    [InlineData(".csv", 80)]
    public void ForExtension_ReturnsValidImageSource_WithProperPackUri(string ext, int minDimension)
    {
        RunOnSta(() =>
        {
            var img = CategoryGlyphs.ForExtension(ext) as BitmapImage;
            Assert.NotNull(img);
            Assert.Contains("Everything2Everything;component", img.UriSource.OriginalString);
            Assert.True(img.PixelWidth >= minDimension, $"Glyph for {ext} should have width >= {minDimension}");
            Assert.True(img.PixelHeight >= minDimension, $"Glyph for {ext} should have height >= {minDimension}");
        });
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
                    try { _ = new Application(); } catch { }
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

        if (ex != null) throw new AggregateException("STA failure", ex);
    }
}
