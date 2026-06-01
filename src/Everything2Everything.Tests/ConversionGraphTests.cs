using System.IO;
using Everything2Everything.Core;
using Everything2Everything.Core.Providers;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>가짜 Provider — 그래프 경로 탐색 단위 테스트용. 실제 변환은 더미 파일 경로를 반환한다.</summary>
internal sealed class FakeProvider : IConverterProvider
{
    public ProviderCapability Capability { get; }

    public FakeProvider(string id, params ConversionPair[] pairs)
    {
        Capability = new ProviderCapability(
            Id: id,
            DisplayName: id,
            SupportedConversions: pairs,
            Status: ProviderStatus.Available,
            Summary: id,
            ExternalDependencies: Array.Empty<ExternalDependency>());
    }

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
        => Task.FromResult(ConvertResult.Ok(sourcePath,
            new[] { Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(sourcePath) + outputExtension) }));
}

public class ConversionGraphTests
{
    private static ConversionGraph Build(params (string from, string to, LossClass loss)[] edges)
    {
        var g = new ConversionGraph();
        var p = new FakeProvider("fake");
        foreach (var (from, to, loss) in edges)
            g.AddEdge(from, to, p, loss);
        return g;
    }

    [Fact]
    public void DirectEdge_IsSingleHop()
    {
        var g = Build((".a", ".b", LossClass.Recode));
        var path = g.FindBestPath(".a", ".b");
        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal(".a", path![0].From);
        Assert.Equal(".b", path[0].To);
    }

    [Fact]
    public void MultiHop_IsAutoComposed()
    {
        var g = Build((".a", ".b", LossClass.Recode), (".b", ".c", LossClass.Recode));
        var path = g.FindBestPath(".a", ".c");
        Assert.NotNull(path);
        Assert.Equal(2, path!.Count);
        Assert.Equal(".a", path[0].From);
        Assert.Equal(".b", path[0].To);
        Assert.Equal(".c", path[1].To);
    }

    [Fact]
    public void NoPath_ReturnsNull()
    {
        var g = Build((".a", ".b", LossClass.Recode));
        Assert.Null(g.FindBestPath(".a", ".z"));
        Assert.Null(g.FindBestPath(".x", ".b"));
    }

    [Fact]
    public void SameFormat_SelfEdge_IsReturned()
    {
        // pdf→pdf 압축 같은 동일포맷 self-edge는 경로로 반환된다.
        var g = Build((".pdf", ".pdf", LossClass.Container));
        var path = g.FindBestPath(".pdf", ".pdf");
        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal(".pdf", path![0].To);
    }

    [Fact]
    public void SameFormat_WithoutSelfEdge_ReturnsNull()
    {
        // self-edge가 없으면 동일포맷은 경로 없음 → 엔진이 Skip 처리한다.
        var g = Build((".a", ".b", LossClass.Recode));
        Assert.Null(g.FindBestPath(".a", ".a"));
    }

    [Fact]
    public void MaxHops_LimitsPathLength()
    {
        var g = Build((".a", ".b", LossClass.Recode), (".b", ".c", LossClass.Recode), (".c", ".d", LossClass.Recode));
        Assert.NotNull(g.FindBestPath(".a", ".d", maxHops: 3));
        Assert.Null(g.FindBestPath(".a", ".d", maxHops: 2));
    }

    [Fact]
    public void AvoidLossy_SkipsRasterizeEdges()
    {
        // a→z 직접은 Rasterize, a→b→z는 무손실 경로. allowLossy=false면 후자만.
        var g = Build(
            (".a", ".z", LossClass.Rasterize),
            (".a", ".b", LossClass.Lossless),
            (".b", ".z", LossClass.Lossless));

        var lossy = g.FindBestPath(".a", ".z", allowLossy: true);
        Assert.NotNull(lossy);

        var safe = g.FindBestPath(".a", ".z", maxHops: 3, allowLossy: false);
        Assert.NotNull(safe);
        Assert.Equal(2, safe!.Count); // 래스터 직접 엣지를 피해 우회
    }

    [Fact]
    public void LowestLoss_PathIsPreferred()
    {
        // a→c 직접(Rasterize, 큰 손실) vs a→b→c(둘 다 Lossless). Dijkstra는 무손실 우회를 택한다.
        var g = Build(
            (".a", ".c", LossClass.Rasterize),
            (".a", ".b", LossClass.Lossless),
            (".b", ".c", LossClass.Lossless));
        var path = g.FindBestPath(".a", ".c");
        Assert.NotNull(path);
        Assert.Equal(2, path!.Count); // 손실 적은 멀티홉 선호
    }

    [Fact]
    public void ReachableOutputs_ComputesTransitiveClosure()
    {
        var g = Build((".a", ".b", LossClass.Recode), (".b", ".c", LossClass.Recode), (".c", ".d", LossClass.Recode));
        var reach = g.ReachableOutputs(".a", maxHops: 3);
        Assert.Contains(".b", reach);
        Assert.Contains(".c", reach);
        Assert.Contains(".d", reach);
    }

    [Fact]
    public void HopConstrained_NotPoisonedByCheaperLongerRoute()
    {
        // 회귀: .a→.x→.b(무손실 2홉, 저비용)가 .b의 홉을 2로 오염시켜
        // .a→.b→.goal(2홉) 유효 경로를 막던 false negative 방지. (적대적 리뷰 confirmed #1)
        var g = Build(
            (".a", ".x", LossClass.Lossless),
            (".x", ".b", LossClass.Lossless),
            (".a", ".b", LossClass.Recode),
            (".b", ".goal", LossClass.Recode));
        var path = g.FindBestPath(".a", ".goal", maxHops: 2);
        Assert.NotNull(path);
        Assert.Equal(2, path!.Count);
        Assert.Equal(".a", path[0].From);
        Assert.Equal(".b", path[0].To);
        Assert.Equal(".goal", path[1].To);
    }

    [Fact]
    public void ReachableOutputs_AllHaveFindablePath()
    {
        // 불변식: ReachableOutputs가 반환한 모든 출력은 FindBestPath로 실제 경로가 나와야 한다. (confirmed #2)
        var g = Build(
            (".a", ".b", LossClass.Recode),
            (".b", ".c", LossClass.Recode),
            (".a", ".x", LossClass.Lossless),
            (".x", ".c", LossClass.Lossless),
            (".c", ".d", LossClass.Recode));
        foreach (var outExt in g.ReachableOutputs(".a", maxHops: 3))
            Assert.NotNull(g.FindBestPath(".a", outExt, maxHops: 3));
    }

    [Fact]
    public void PairsFromMatrix_ExcludesSelfPairs()
    {
        // 회귀: 자기쌍(png→png)이 제외되어 동일포맷 재인코딩이 발생하지 않는다. (confirmed #8)
        var pairs = ProviderCapability.PairsFromMatrix(
            new[] { ".png", ".jpg" }, new[] { ".png", ".jpg", ".webp" });
        Assert.DoesNotContain(pairs, p => p.InputExtension == p.OutputExtension);
        Assert.Contains(pairs, p => p.InputExtension == ".png" && p.OutputExtension == ".webp");
    }
}

public class RegistryGraphIntegrationTests
{
    [Fact]
    public void DefaultEngine_BuildsGraph_WithKnownEdges()
    {
        var engine = Everything2EverythingBootstrap.CreateDefault();
        var graph = engine.Providers.Graph;

        // PdfToolProvider가 pdf→pdf 압축 엣지를 등록했는지 (P1 신규)
        var pdfCompress = graph.FindBestPath(".pdf", ".pdf");
        Assert.NotNull(pdfCompress);

        // 기존 변환 능력이 그래프에 반영되는지 (png은 입력 노드로 존재)
        Assert.True(graph.HasNode(".png"));
    }

    [Fact]
    public void DefaultEngine_PngReachesPdf()
    {
        var engine = Everything2EverythingBootstrap.CreateDefault();
        var graph = engine.Providers.Graph;
        // png→pdf 경로(직접 또는 멀티홉)가 존재해야 한다.
        var path = graph.FindBestPath(".png", ".pdf", maxHops: 3);
        Assert.NotNull(path);
    }
}
