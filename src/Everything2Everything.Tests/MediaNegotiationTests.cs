using System.IO;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Filters;
using Everything2Everything.Core.Providers;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 미디어 전환 가능 Filter 네고시에이팅 기능 테스트 스위트 (TDD).
/// PNG 등 이미지 입력에 대해 DOCX/Word 메뉴 및 변환이 노출/허용되지 않도록 보장한다.
/// </summary>
public class MediaNegotiationTests
{
    private static ConversionEngine CreateEngine() => Everything2EverythingBootstrap.CreateDefault();
    private static ProviderRegistry CreateRegistry() => CreateEngine().Providers;

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".webp")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    public void OutputsForInput_ImageFormats_DoNotContain_Docx_Or_OtherRichDocuments(string imageExt)
    {
        var reg = CreateRegistry();
        var outputs = reg.OutputsForInput(imageExt);

        // 이미지 입력은 Word(.docx, .doc), HWP, HTML, Markdown 등의 서식 문서로 변환될 수 없다.
        Assert.DoesNotContain(".docx", outputs);
        Assert.DoesNotContain(".doc", outputs);
        Assert.DoesNotContain(".hwp", outputs);
        Assert.DoesNotContain(".hwpx", outputs);
        Assert.DoesNotContain(".html", outputs);
        Assert.DoesNotContain(".md", outputs);
    }

    [Fact]
    public void AvailableOutputsForFiles_PngQueue_DoesNotContain_Docx()
    {
        var reg = CreateRegistry();
        var available = reg.AvailableOutputsForFiles(new[] { "C:\\test\\sample.png" });

        // PNG 큐에 대해 UI 및 컨텍스트 메뉴용 공통 출력 목록에 .docx가 포함되지 않아야 한다.
        Assert.DoesNotContain(".docx", available);
    }

    [Fact]
    public void OutputsForInput_Png_Maintains_ValidOutputs()
    {
        var reg = CreateRegistry();
        var outputs = reg.OutputsForInput(".png");

        // 이미지 간 변환은 정상 유지
        Assert.Contains(".jpg", outputs);
        Assert.Contains(".webp", outputs);

        // 이미지 캡슐화 PDF는 정상 유지
        Assert.Contains(".pdf", outputs);

        // OCR 텍스트 추출은 정상 유지
        Assert.Contains(".txt", outputs);
    }

    [Fact]
    public void OutputsForInput_Docx_Maintains_DocumentAndImageOutputs()
    {
        var reg = CreateRegistry();
        var outputs = reg.OutputsForInput(".docx");

        // DOCX는 PDF 변환 및 렌더링 이미지 출력이 가능해야 함
        Assert.Contains(".pdf", outputs);
        Assert.Contains(".txt", outputs);
    }

    [Fact]
    public async Task ConvertOneAsync_PngToDocx_FailsWithUnsupportedMediaConversion()
    {
        var engine = CreateEngine();
        var tempPng = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tempPng, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // minimal png header

        try
        {
            var result = await engine.ConvertOneAsync(tempPng, ".docx", new ConvertOptions());
            Assert.Equal(ConvertStatus.Failed, result.Status);
            Assert.Contains("미디어 전환", result.Message);
        }
        finally
        {
            if (File.Exists(tempPng)) File.Delete(tempPng);
        }
    }

    [Fact]
    public void ConversionGraph_FindBestPath_PngToDocx_ReturnsNull()
    {
        var graph = CreateEngine().Providers.Graph;
        var path = graph.FindBestPath(".png", ".docx", maxHops: 3);
        Assert.Null(path);
    }

    [Theory]
    [InlineData(".png", ".docx", false)]
    [InlineData(".jpg", ".docx", false)]
    [InlineData(".png", ".hwp", false)]
    [InlineData(".png", ".jpg", true)]
    [InlineData(".png", ".pdf", true)]
    [InlineData(".png", ".txt", true)]
    [InlineData(".docx", ".pdf", true)]
    [InlineData(".docx", ".png", true)]
    [InlineData(".mp4", ".docx", false)]
    [InlineData(".csv", ".docx", false)]
    public void MediaConversionNegotiator_ValidatesCompatibility(string input, string output, bool expectedAllowed)
    {
        var allowed = MediaConversionNegotiator.CanConvert(input, output);
        Assert.Equal(expectedAllowed, allowed);
    }

    [Theory]
    [InlineData(".mp3", ".gif", false)]
    [InlineData(".mp3", ".png", false)]
    [InlineData(".mp3", ".jpg", false)]
    [InlineData(".mp3", ".mp4", false)]
    [InlineData(".wav", ".mkv", false)]
    [InlineData(".mp3", ".wav", true)]
    [InlineData(".flac", ".mp3", true)]
    [InlineData(".wav", ".aac", true)]
    [InlineData(".mp4", ".mkv", true)]
    [InlineData(".mp4", ".mp3", true)]
    [InlineData(".mp4", ".gif", true)]
    [InlineData(".mp4", ".png", true)]
    [InlineData(".mp4", ".jpg", true)]
    [InlineData(".mkv", ".webp", true)]
    [InlineData(".mp4", ".pdf", false)]
    [InlineData(".mp4", ".docx", false)]
    [InlineData(".mp4", ".csv", false)]
    public void MediaConversionNegotiator_DistinguishesVideoAndAudio(string input, string output, bool expectedAllowed)
    {
        var allowed = MediaConversionNegotiator.CanConvert(input, output);
        Assert.Equal(expectedAllowed, allowed);
    }

    [Fact]
    public void AvailableOutputsForFiles_IncompatibleMixedQueue_ReturnsEmpty()
    {
        var reg = CreateRegistry();
        // mp3와 csv는 공통 변환 출력이 전혀 없다
        var available = reg.AvailableOutputsForFiles(new[] { "C:\\music.mp3", "C:\\data.csv" });
        Assert.Empty(available);
    }
}
