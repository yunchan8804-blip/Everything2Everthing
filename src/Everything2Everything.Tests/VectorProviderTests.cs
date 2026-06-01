using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Everything2Everything.Core.Providers;
using Xunit;

namespace Everything2Everything.Tests;

public class VectorProviderTests
{
    private const string Svg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect width='100' height='80' fill='red'/><circle cx='50' cy='40' r='20' fill='blue'/></svg>";

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "e2e_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public async Task Svg_To_Png_ProducesValidFile()
    {
        var dir = TempDir();
        var svg = Path.Combine(dir, "t.svg");
        await File.WriteAllTextAsync(svg, Svg);

        var p = new VectorProvider();
        var r = await p.ConvertAsync(svg, dir, ".png", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.True(new FileInfo(r.OutputPaths[0]).Length > 100);
    }

    [Fact]
    public async Task Svg_To_Pdf_ProducesValidFile()
    {
        var dir = TempDir();
        var svg = Path.Combine(dir, "t.svg");
        await File.WriteAllTextAsync(svg, Svg);

        var p = new VectorProvider();
        var r = await p.ConvertAsync(svg, dir, ".pdf", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.True(new FileInfo(r.OutputPaths[0]).Length > 100);
    }

    [Fact]
    public void DefaultGraph_SvgToJpg_ComposedViaPng()
    {
        var graph = Everything2EverythingBootstrap.CreateDefault().Providers.Graph;
        var path = graph.FindBestPath(".svg", ".jpg", maxHops: 3);
        Assert.NotNull(path); // svg→png→jpg 자동 합성
        var route = string.Join(" -> ", path!.Select(e => e.From)) + " -> " + path[^1].To;
        Assert.True(path.Count == 2, "예상 svg→png→jpg(2홉), 실제: " + route);
    }
}
