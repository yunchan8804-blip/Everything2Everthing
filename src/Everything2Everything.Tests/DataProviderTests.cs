using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Everything2Everything.Core.Providers;
using Xunit;

namespace Everything2Everything.Tests;

public class DataProviderTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "e2e_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public async Task Csv_To_Json_To_Csv_Roundtrip()
    {
        var dir = TempDir();
        var csv = Path.Combine(dir, "t.csv");
        await File.WriteAllTextAsync(csv, "name,age\nAlice,30\nBob,25\n");

        var p = new DataProvider();
        var r1 = await p.ConvertAsync(csv, dir, ".json", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r1.Status);
        var json = await File.ReadAllTextAsync(r1.OutputPaths[0]);
        Assert.Contains("Alice", json);
        Assert.Contains("30", json);

        var r2 = await p.ConvertAsync(r1.OutputPaths[0], dir, ".csv", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r2.Status);
        var csv2 = await File.ReadAllTextAsync(r2.OutputPaths[0]);
        Assert.Contains("Alice", csv2);
        Assert.Contains("age", csv2);
    }

    [Fact]
    public async Task Csv_To_Xlsx_To_Csv_Roundtrip()
    {
        var dir = TempDir();
        var csv = Path.Combine(dir, "t.csv");
        await File.WriteAllTextAsync(csv, "a,b\n1,x\n2,y\n");

        var p = new DataProvider();
        var r1 = await p.ConvertAsync(csv, dir, ".xlsx", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r1.Status);
        Assert.True(new FileInfo(r1.OutputPaths[0]).Length > 0);

        var r2 = await p.ConvertAsync(r1.OutputPaths[0], dir, ".csv", new ConvertOptions(), null, CancellationToken.None);
        Assert.Equal(ConvertStatus.Success, r2.Status);
        var csv2 = await File.ReadAllTextAsync(r2.OutputPaths[0]);
        Assert.Contains("a", csv2);
        Assert.Contains("x", csv2);
    }

    [Fact]
    public void DefaultGraph_JsonToXlsx_ComposedViaCsv()
    {
        var graph = Everything2EverythingBootstrap.CreateDefault().Providers.Graph;
        var path = graph.FindBestPath(".json", ".xlsx", maxHops: 3);
        Assert.NotNull(path);
        Assert.Equal(2, path!.Count); // json→csv→xlsx 자동 합성
    }
}
