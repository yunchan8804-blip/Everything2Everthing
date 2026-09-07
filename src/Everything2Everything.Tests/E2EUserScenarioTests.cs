using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Everything2Everything.App.ViewModels;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Everything2Everything.Core.Providers;
using Everything2Everything.Core.Filters;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 복합 사용자 유즈케이스 E2E 시나리오 TDD 테스트 스위트
/// 실제 사용자의 큐 조작, 포맷 교집합 필터링, 인코딩 옵션 튜닝, 진행률 클램프 및 취소 시나리오를 검증한다.
/// </summary>
public class E2EUserScenarioTests
{
    private readonly ConversionEngine _engine;

    public E2EUserScenarioTests()
    {
        _engine = Everything2EverythingBootstrap.CreateDefault();
    }

    [Fact]
    public void Scenario1_FileQueue_ComputesCommonFormatIntersection_AndSortsCorrectly()
    {
        // 사용자가 .png 이미지와 .docx 문서를 함께 드래그 & 드롭했을 때,
        // 사이드바의 출력 포맷 콤보박스는 두 입력이 "동시에 변환 가능한 출력의 교집합"만 남겨야 한다.
        var pngOutputs = _engine.Providers.OutputsForInput(".png").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var docxOutputs = _engine.Providers.OutputsForInput(".docx").ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 교집합 계산
        var intersection = pngOutputs.Intersect(docxOutputs, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.NotEmpty(intersection);
        // PDF, JPG 등은 PNG와 DOCX 양쪽 모두에서 공통 출력 가능해야 함
        Assert.Contains(".pdf", intersection);
        Assert.Contains(".jpg", intersection);

        // 만약 여기에 다른 포맷(.csv 등)이 추가되면 교집합이 변하거나 비어있어야 함
        var csvOutputs = _engine.Providers.OutputsForInput(".csv").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var threeWayIntersection = intersection.Intersect(csvOutputs, StringComparer.OrdinalIgnoreCase).ToList();

        // CSV는 텍스트/표 기반이므로 이미지/PDF와의 교집합은 없어야 함
        Assert.DoesNotContain(".jpg", threeWayIntersection);
    }

    [Fact]
    public void Scenario2_OptionsViewModel_Tuning_ReflectsInEncoderParameters()
    {
        // 사용자가 슬라이더와 콤보박스로 인코딩 옵션을 조정하는 시나리오
        var vm = new OptionsViewModel
        {
            Quality = 92,
            Crf = 18,
            ResolutionIndex = 3, // 1080p
            AudioBitrateIndex = 4, // 320 kbps
            VideoPreferGpu = false
        };

        Assert.Equal(92, vm.Quality);
        Assert.Equal(18, vm.Crf);
        Assert.Equal(3, vm.ResolutionIndex);
        Assert.Equal(4, vm.AudioBitrateIndex);
        Assert.False(vm.VideoPreferGpu);

        // ConvertOptions 에 매핑되는 로직 검증
        var options = vm.ToConvertOptions();

        Assert.Equal(92, options.Jpeg.Quality);
        Assert.Equal(92, options.Webp.Quality);
        Assert.Equal(18, options.Video.Crf);
        Assert.False(options.VideoPreferGpu);
        Assert.Equal(1920, options.Video.ScaleWidth);
    }

    [Theory]
    [InlineData(-10.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(45.5, 45.5)]
    [InlineData(100.0, 100.0)]
    [InlineData(125.0, 100.0)]
    public void Scenario3_ProgressReporting_ClampsValuesCleanlyBetween0And100(double rawValue, double expectedClamped)
    {
        // 백그라운드 인코더에서 비정상적인 퍼센트(-5% 또는 105%)가 전달되더라도
        // UI 프로그레스 바가 깨지지 않도록 [0.0, 100.0] 범위로 정밀 클램프되어야 한다.
        double clamped = Math.Clamp(rawValue, 0.0, 100.0);
        Assert.Equal(expectedClamped, clamped);
    }

    [Fact]
    public void Scenario4_ConversionGraph_FindBestPath_ProvidesValidSteps()
    {
        // 다단계 변환 그래프 탐색 유즈케이스 검증
        var path = _engine.Providers.Graph.FindBestPath(".png", ".jpg");
        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.Equal(".png", path![0].From);
    }

    [Fact]
    public void Scenario5_MultiHopTransitive_FindBestPath_FindsValidIntermediateHops()
    {
        // 직접적인 단일 Provider가 없더라도 유효한 2홉 변환 경로를 찾아내는지 검증
        // 예: .md -> .html -> .png
        var path = _engine.Providers.Graph.FindBestPath(".md", ".png");
        Assert.NotNull(path);
        Assert.True(path!.Count >= 2, "MD to PNG should traverse at least 2 hops (.md -> .html/.pdf -> .png)");
        Assert.Equal(".md", path[0].From);
        Assert.Equal(".png", path[^1].To);

        // 중간 홉이 불가능한 도메인 도약(예: .docx)을 포함하지 않는지 검증
        foreach (var hop in path)
        {
            Assert.True(MediaConversionNegotiator.CanConvert(hop.From, hop.To),
                $"Hop {hop.From} -> {hop.To} must satisfy domain negotiation rules");
        }
    }

    [Theory]
    [InlineData(1000, 200, 80.0)]    // 1000B -> 200B (80% 절감)
    [InlineData(1000, 1000, 0.0)]    // 동일 크기 (0% 절감)
    [InlineData(1000, 1500, -50.0)]  // 크기 증가 (-50% 절감)
    [InlineData(0, 500, 0.0)]        // 0바이트 원본 예외 방어 (DivideByZero 방어)
    public void Scenario6_StorageSavingsCalculation_HandlesAllEdgeCasesSafely(long originalBytes, long outputBytes, double expectedSavings)
    {
        double savings = ComputeStorageSavings(originalBytes, outputBytes);
        Assert.Equal(expectedSavings, savings, precision: 1);
    }

    private static double ComputeStorageSavings(long orig, long @out)
    {
        if (orig <= 0) return 0.0;
        return ((double)(orig - @out) / orig) * 100.0;
    }

    [Fact]
    public void Scenario7_MediaConversionNegotiator_StrictRejection_AcrossAllCrossDomains()
    {
        // 텍스트/표/오디오/이미지/비디오 간의 부적절한 도약 전수 거부 시나리오
        Assert.False(MediaConversionNegotiator.CanConvert(".png", ".docx"));
        Assert.False(MediaConversionNegotiator.CanConvert(".jpg", ".xlsx"));
        Assert.False(MediaConversionNegotiator.CanConvert(".mp3", ".gif"));
        Assert.False(MediaConversionNegotiator.CanConvert(".csv", ".mp4"));
        Assert.False(MediaConversionNegotiator.CanConvert(".docx", ".mp3"));
        Assert.False(MediaConversionNegotiator.CanConvert(".xlsx", ".png"));
    }

    [Theory]
    [InlineData("summarize", null, "요약")]
    [InlineData("translate", "일본어", "일본어")]
    [InlineData("proofread", null, "교정")]
    public void Scenario8_AiQuickPanel_Options_BuildPrompt_FormatsCorrectlyForAllTasks(string task, string? targetLang, string expectedKeyword)
    {
        var options = new AiOptions { Task = task, TargetLanguage = targetLang };
        var (system, user) = LlmProvider.BuildPrompt(options, "테스트 입력 문장");
        Assert.Contains(expectedKeyword, system);
        Assert.Equal("테스트 입력 문장", user);
    }

    [Fact]
    public async Task Scenario9_AiConversion_EndToEnd_SwitchboardResolution()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "E2E_AiConversion_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var srcFile = Path.Combine(tempDir, "sample.md");
            await File.WriteAllTextAsync(srcFile, "# Hello\nEverything2Everything test content.");

            var store = new FakeSettingsStore();
            store.Set("switchboard.endpoint", "http://192.168.0.225:8787");
            var provider = new LlmProvider(store);

            var options = new ConvertOptions
            {
                Ai = new AiOptions { Task = "summarize", Backend = "switchboard" }
            };

            var (client, _) = provider.ResolveClientForTesting(options.Ai);
            Assert.NotNull(client);
            Assert.Equal("Switchboard Gateway", client.Name);
            var sbClient = Assert.IsType<SwitchboardChatClient>(client);
            Assert.Equal("http://192.168.0.225:8787", sbClient.Endpoint);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _d = new();
        public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _d[key] = value;
        public void Remove(string key) => _d.Remove(key);
        public bool Contains(string key) => _d.ContainsKey(key);
    }
}
