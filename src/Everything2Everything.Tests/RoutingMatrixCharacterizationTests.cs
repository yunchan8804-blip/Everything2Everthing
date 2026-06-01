using System.Linq;
using Everything2Everything.Core;
using Everything2Everything.Core.Providers;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 기본 엔진의 실제 변환 그래프에 대한 라우팅 매트릭스 특성화. FindBestPath/ReachableOutputs는 순수 함수라
/// 비결정성이 없다. UI 추천 출력(커밋 573d045)과 멀티홉 자동 합성의 현재 동작을 박제해
/// 리팩토링(P2~P6)이 라우팅을 바꾸면 즉시 드러나게 한다.
/// </summary>
public class RoutingMatrixCharacterizationTests
{
    private static ConversionGraph Graph() => Everything2EverythingBootstrap.CreateDefault().Providers.Graph;
    private static ProviderRegistry Registry() => Everything2EverythingBootstrap.CreateDefault().Providers;

    [Theory]
    // 이미지 직접 변환 (magick, 1홉)
    [InlineData(".png", ".jpg", 1)]
    [InlineData(".jpg", ".png", 1)]
    [InlineData(".png", ".webp", 1)]
    [InlineData(".png", ".pdf", 1)]
    [InlineData(".pdf", ".png", 1)]   // 래스터 (pdf provider)
    // 벡터 (vector provider)
    [InlineData(".svg", ".png", 1)]
    [InlineData(".svg", ".pdf", 1)]
    [InlineData(".svg", ".jpg", 2)]   // 멀티홉 svg→png→jpg
    // 표 데이터 (data provider, 도그푸딩)
    [InlineData(".csv", ".json", 1)]
    [InlineData(".json", ".csv", 1)]
    [InlineData(".csv", ".xlsx", 1)]
    [InlineData(".xlsx", ".csv", 1)]
    [InlineData(".json", ".xlsx", 2)] // 멀티홉 json→csv→xlsx
    public void FindBestPath_HasExpectedHopCount(string input, string output, int expectedHops)
    {
        var path = Graph().FindBestPath(input, output, maxHops: 3);
        Assert.NotNull(path);
        Assert.Equal(expectedHops, path!.Count);
        Assert.Equal(ConversionPair.Normalize(input), path[0].From);
        Assert.Equal(ConversionPair.Normalize(output), path[^1].To);
    }

    [Fact]
    public void SelfEdges_AndMissingSelfEdges_AreLocked()
    {
        var g = Graph();
        Assert.NotNull(g.FindBestPath(".png", ".png")); // imageoptim 재압축 self-edge
        Assert.NotNull(g.FindBestPath(".jpg", ".jpg")); // imageoptim 재압축 self-edge
        Assert.NotNull(g.FindBestPath(".pdf", ".pdf")); // pdftool 압축 self-edge
        Assert.Null(g.FindBestPath(".csv", ".csv"));    // data provider는 csv self-edge 없음 → 엔진 Skip
    }

    [Fact]
    public void AvoidLossy_DropsRasterizeOnlyRoutes()
    {
        var g = Graph();
        // pdf→png 유일 경로는 래스터화. allowLossy=false면 경로가 없어야 한다.
        Assert.NotNull(g.FindBestPath(".pdf", ".png", maxHops: 3, allowLossy: true));
        Assert.Null(g.FindBestPath(".pdf", ".png", maxHops: 3, allowLossy: false));

        // png→jpg는 Recode(래스터 아님) → allowLossy=false여도 유지된다.
        Assert.NotNull(g.FindBestPath(".png", ".jpg", maxHops: 1, allowLossy: false));
    }

    [Fact]
    public void OutputsForInput_ExposesUiRecommendedSets()
    {
        var reg = Registry();

        var png = reg.OutputsForInput(".png");
        Assert.Contains(".jpg", png);
        Assert.Contains(".webp", png);
        Assert.Contains(".pdf", png);
        Assert.DoesNotContain(".png", png); // 동일포맷 self는 '다른 형식 변환' 목록에서 제외

        var svg = reg.OutputsForInput(".svg");
        Assert.Contains(".png", svg);
        Assert.Contains(".jpg", svg); // 멀티홉 노출
        Assert.Contains(".pdf", svg);

        var json = reg.OutputsForInput(".json");
        Assert.Contains(".csv", json);
        Assert.Contains(".xlsx", json); // 멀티홉 json→csv→xlsx
        Assert.DoesNotContain(".json", json);
    }

    [Fact]
    public void ReachableOutputs_InvariantHoldsOnRealGraph()
    {
        var g = Graph();
        // 불변식: ReachableOutputs가 반환한 모든 출력은 FindBestPath로 실제 경로가 나와야 한다.
        // (UI 노출은 2홉 기준 — ProviderRegistry.OutputsForInput과 동일)
        foreach (var input in new[] { ".png", ".jpg", ".svg", ".csv", ".json", ".pdf" })
            foreach (var outExt in g.ReachableOutputs(input, maxHops: 2, allowLossy: true))
                Assert.NotNull(g.FindBestPath(input, outExt, maxHops: 2));
    }
}
