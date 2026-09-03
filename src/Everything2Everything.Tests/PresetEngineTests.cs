using Everything2Everything.App.ViewModels;
using Everything2Everything.Core.Presets;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 1-클릭 빠른 최적화 프리셋 시스템 TDD 단위 테스트 스위트
/// 웹 최적화, 무손실 보존, 문서 PDF 보관, 모바일 공유 프리셋의 파라미터 튜닝 및 추천 확장자 검증
/// </summary>
public class PresetEngineTests
{
    [Fact]
    public void Apply_WebOptimized_SetsWebPAndOptimalCompression()
    {
        var options = new OptionsViewModel();
        var targetExt = ConversionPreset.Apply(PresetType.WebOptimized, options, ".png");

        Assert.Equal(".webp", targetExt);
        Assert.Equal(80, options.ImageQuality);
        Assert.True(options.StripMetadata);
    }

    [Fact]
    public void Apply_HighQualityLossless_SetsMaxQualityAndFlacOrPng()
    {
        var options = new OptionsViewModel();
        var audioTarget = ConversionPreset.Apply(PresetType.HighQualityLossless, options, ".wav");
        Assert.Equal(".flac", audioTarget);
        Assert.Equal(320, options.AudioBitrateKbps);

        var imgTarget = ConversionPreset.Apply(PresetType.HighQualityLossless, options, ".jpg");
        Assert.Equal(".png", imgTarget);
        Assert.Equal(100, options.ImageQuality);
        Assert.False(options.StripMetadata);
    }

    [Fact]
    public void Apply_DocumentPdf_SetsPdfTargetForDocuments()
    {
        var options = new OptionsViewModel();
        var docxTarget = ConversionPreset.Apply(PresetType.DocumentPdf, options, ".docx");
        Assert.Equal(".pdf", docxTarget);

        var hwpTarget = ConversionPreset.Apply(PresetType.DocumentPdf, options, ".hwp");
        Assert.Equal(".pdf", hwpTarget);
    }

    [Fact]
    public void Apply_MobileShare_SetsLightweightVideoAndAudio()
    {
        var options = new OptionsViewModel();
        var videoTarget = ConversionPreset.Apply(PresetType.MobileShare, options, ".mkv");
        Assert.Equal(".mp4", videoTarget);
        Assert.Equal(26, options.VideoCrf);
        Assert.Equal(128, options.AudioBitrateKbps);
        Assert.Equal("veryfast", options.VideoPreset);
    }

    [Theory]
    [InlineData(PresetType.WebOptimized, "웹 최적화", "WebP · 80% 압축 · 메타데이터 제거")]
    [InlineData(PresetType.HighQualityLossless, "초고화질 보존", "무손실 PNG/FLAC · 100% 품질")]
    [InlineData(PresetType.DocumentPdf, "문서 PDF 보관", "표준 PDF/A 문서 변환")]
    [InlineData(PresetType.MobileShare, "모바일 공유", "MP4 H.264 · 가벼운 용량 전송")]
    public void Presets_HaveUserFacingTitleAndDescription(PresetType type, string expectedTitle, string expectedDesc)
    {
        var info = ConversionPreset.GetInfo(type);
        Assert.Equal(expectedTitle, info.Title);
        Assert.Equal(expectedDesc, info.Description);
    }
}
