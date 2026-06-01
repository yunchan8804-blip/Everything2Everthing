using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Everything2Everything.Core.Providers;
using ImageMagick;
using Xunit;

namespace Everything2Everything.Tests;

public class ImageOptimProviderTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "e2e_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public async Task Png_Optimize_ProducesOutput()
    {
        var dir = TempDir();
        var png = Path.Combine(dir, "t.png");
        using (var img = new MagickImage(MagickColors.Red, 64, 64)) img.Write(png);

        var p = new ImageOptimProvider();
        var r = await p.ConvertAsync(png, dir, ".png", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.True(File.Exists(r.OutputPaths[0]));
    }

    [Fact]
    public async Task Jpg_Optimize_LowerQuality_ProducesOutput()
    {
        var dir = TempDir();
        var jpg = Path.Combine(dir, "t.jpg");
        using (var img = new MagickImage(MagickColors.SteelBlue, 256, 256)) img.Write(jpg);

        var p = new ImageOptimProvider();
        var options = new ConvertOptions();
        options.Jpeg.Quality = 30;
        var r = await p.ConvertAsync(jpg, dir, ".jpg", options, null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.True(File.Exists(r.OutputPaths[0]));
    }

    [Fact]
    public void DefaultGraph_PngSelfEdge_ExistsForOptimization()
    {
        var graph = Everything2EverythingBootstrap.CreateDefault().Providers.Graph;
        // 이미지 최적화 self-edge가 그래프에 등록되어 png→png가 가능 (압축 의도)
        Assert.NotNull(graph.FindBestPath(".png", ".png"));
        Assert.NotNull(graph.FindBestPath(".jpg", ".jpg"));
    }
}
