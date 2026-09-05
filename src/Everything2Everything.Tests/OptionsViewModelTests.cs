using Everything2Everything.App.ViewModels;
using Everything2Everything.Core;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// OptionsViewModel.ToConvertOptions 단위 테스트 — 기존 MainWindow.BuildOptions(코드비하인드, 테스트 불가)에서
/// 추출한 순수 옵션 구성 로직을 헤드리스로 검증(P5b MVVM). UI 컨트롤 읽기와 분리되어 회귀를 기계적으로 방어.
/// </summary>
public class OptionsViewModelTests
{
    [Fact]
    public void ToConvertOptions_MapsQualityToImageFormats()
    {
        var o = new OptionsViewModel { Quality = 70 }.ToConvertOptions();
        Assert.Equal(70, o.Jpeg.Quality);
        Assert.Equal(70, o.Webp.Quality);
        Assert.Equal(40, o.Avif.Quality); // AVIF = Quality - 30
    }

    [Fact]
    public void ToConvertOptions_AvifQualityClampedToFloor()
    {
        Assert.Equal(1, new OptionsViewModel { Quality = 10 }.ToConvertOptions().Avif.Quality);
    }

    [Fact]
    public void ToConvertOptions_EmptyCustomDir_UsesSubfolder()
    {
        var o = new OptionsViewModel { CustomOutputDirectory = "   " }.ToConvertOptions();
        Assert.Equal(OutputLocation.SubfolderBesideSource, o.OutputLocation);
        Assert.Null(o.CustomOutputDirectory);
    }

    [Fact]
    public void ToConvertOptions_CustomDir_TrimmedAndCustomLocation()
    {
        var o = new OptionsViewModel { CustomOutputDirectory = "  C:\\out  " }.ToConvertOptions();
        Assert.Equal(OutputLocation.Custom, o.OutputLocation);
        Assert.Equal("C:\\out", o.CustomOutputDirectory);
    }

    [Theory]
    [InlineData(0, "summarize")]
    [InlineData(1, "translate")]
    [InlineData(2, "proofread")]
    [InlineData(99, "summarize")]
    public void ToConvertOptions_MapsAiTask(int index, string expected)
    {
        Assert.Equal(expected, new OptionsViewModel { AiTaskIndex = index }.ToConvertOptions().Ai.Task);
    }

    [Fact]
    public void ToConvertOptions_TargetLanguage_EmptyBecomesNullElseTrimmed()
    {
        Assert.Null(new OptionsViewModel { TargetLanguage = "   " }.ToConvertOptions().Ai.TargetLanguage);
        Assert.Equal("일본어", new OptionsViewModel { TargetLanguage = " 일본어 " }.ToConvertOptions().Ai.TargetLanguage);
    }

    [Fact]
    public void ToConvertOptions_PassesConflictAndGpu()
    {
        var o = new OptionsViewModel { ConflictRule = NameCollision.Skip, VideoPreferGpu = false }.ToConvertOptions();
        Assert.Equal(NameCollision.Skip, o.OnCollision);
        Assert.False(o.VideoPreferGpu);
    }

    [Fact]
    public void OptionsViewModel_HasAdvancedProperties_And_DefaultExpandedIsTrue()
    {
        // 사용자 피드백 반영: 슬라이더와 상세 옵션을 바로 확인할 수 있도록 기본값으로 펼침(true) 상태여야 한다.
        var vm = new OptionsViewModel();
        Assert.True(vm.IsAdvancedExpanded);
        Assert.Equal(0, vm.PdfCompressLevelIndex);
        Assert.Equal(1, vm.PdfDpiIndex);
        Assert.False(vm.ImageLossless);
        Assert.False(vm.Progressive);

        vm.ImageLossless = true;
        vm.Progressive = true;
        vm.PdfCompressLevelIndex = 1;
        vm.PdfDpiIndex = 2;

        var options = vm.ToConvertOptions();
        Assert.True(options.Webp.Lossless);
        Assert.True(options.Jpeg.Progressive);
        Assert.Equal(300, options.PdfRender.Dpi);
    }
}
