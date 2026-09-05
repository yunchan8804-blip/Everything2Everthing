using Everything2Everything.App.ViewModels;
using Everything2Everything.Core.Presets;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 출력 형식 연동 스마트 프리셋 엔진 TDD 단위 테스트 (RED -> GREEN)
/// </summary>
public class FormatPresetEngineTests
{
    [Theory]
    [InlineData(".mp3", "최고음질 320k", 320)]
    [InlineData(".m4a", "최고음질 320k", 320)]
    [InlineData(".aac", "표준 192k", 192)]
    public void GetPresetsForExtension_Audio_ReturnsAudioSpecificPresets(string ext, string expectedTitle, int expectedBitrate)
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(ext);
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Title.Contains(expectedTitle));

        var targetPreset = presets.First(p => p.Title.Contains(expectedTitle));
        Assert.NotEmpty(targetPreset.SpecChips);

        var options = new OptionsViewModel();
        targetPreset.Apply(options);
        Assert.Equal(expectedBitrate, options.AudioBitrateKbps);
    }

    [Fact]
    public void GetPresetsForExtension_WebP_ReturnsImageQualityPresets()
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(".webp");
        Assert.True(presets.Count >= 3);

        var q85 = presets.First(p => p.Title.Contains("웹 고화질"));
        Assert.Contains("품질 85", q85.SpecChips);

        var options = new OptionsViewModel();
        q85.Apply(options);
        Assert.Equal(85, options.ImageQuality);
        Assert.True(options.StripMetadata);
    }

    [Theory]
    [InlineData(".jpg", "품질 95")]
    [InlineData(".webp", "품질 85")]
    [InlineData(".avif", "품질 55")]
    public void GetPresetsForExtension_ImagePresets_MustUseKoreanQualityChips(string ext, string expectedChip)
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(ext);
        var hasExpectedChip = presets.Any(p => p.SpecChips.Contains(expectedChip));
        Assert.True(hasExpectedChip, $"{ext} 프리셋 스펙 칩에 한국어 '{expectedChip}'이 포함되어야 합니다.");

        // 영어 Quality X 칩 금지
        foreach (var p in presets)
        {
            Assert.DoesNotContain(p.SpecChips, c => c.StartsWith("Quality "));
        }
    }

    [Fact]
    public void GetPresetsForExtension_Pdf_ReturnsResolutionPresets()
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(".pdf");
        Assert.True(presets.Count >= 3);

        var print300 = presets.First(p => p.Title.Contains("인쇄용") || p.Title.Contains("300"));
        Assert.Contains(print300.SpecChips, c => c.Contains("300 DPI"));

        var options = new OptionsViewModel();
        print300.Apply(options);
        Assert.Equal(100, options.Quality);
    }

    [Fact]
    public void GetPresetsForExtension_Video_ReturnsCrfPresets()
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(".mp4");
        Assert.True(presets.Count >= 3);

        var web1080 = presets.First(p => p.Title.Contains("1080p"));
        Assert.Contains(web1080.SpecChips, c => c.Contains("1080p") || c.Contains("H.264"));

        var options = new OptionsViewModel();
        web1080.Apply(options);
        Assert.Equal(23, options.VideoCrf);
        Assert.Equal("fast", options.VideoPreset);
    }

    [Theory]
    [InlineData(".jpg", 4)]
    [InlineData(".png", 3)]
    [InlineData(".webp", 4)]
    [InlineData(".avif", 3)]
    [InlineData(".pdf", 4)]
    [InlineData(".mp4", 4)]
    [InlineData(".mp3", 4)]
    [InlineData(".gif", 2)]
    public void GetPresetsForExtension_MajorFormats_ReturnRichPresetSuite(string ext, int minPresetCount)
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(ext);
        Assert.True(presets.Count >= minPresetCount,
            $"{ext} 형식의 프리셋 개수가 너무 적습니다 (최소 {minPresetCount}개 이상 필요, 실제: {presets.Count}개).");

        foreach (var p in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Id), "프리셋 ID가 비어있습니다.");
            Assert.False(string.IsNullOrWhiteSpace(p.Title), "프리셋 제목이 비어있습니다.");
            Assert.False(string.IsNullOrWhiteSpace(p.Description), "프리셋 설명이 비어있습니다.");
            Assert.True(p.SpecChips.Count >= 2, $"프리셋 [{p.Title}]의 스펙 칩 개수가 2개 미만입니다.");
        }
    }

    [Fact]
    public void GetPresetsForExtension_UnknownExtension_ReturnsFallbackPresets()
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(".xyz");
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Title.Contains("기본"));
    }
}
