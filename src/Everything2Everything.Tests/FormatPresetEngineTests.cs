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
        Assert.Contains("Quality 85", q85.SpecChips);

        var options = new OptionsViewModel();
        q85.Apply(options);
        Assert.Equal(85, options.ImageQuality);
        Assert.True(options.StripMetadata);
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

    [Fact]
    public void GetPresetsForExtension_UnknownExtension_ReturnsFallbackPresets()
    {
        var presets = FormatPresetEngine.GetPresetsForExtension(".xyz");
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Title.Contains("기본"));
    }
}
