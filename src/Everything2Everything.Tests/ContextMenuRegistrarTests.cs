using System.Linq;
using Everything2Everything.App.Shell;
using Everything2Everything.Core;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 윈도우 탐색기 우클릭 컨텍스트 메뉴 네고시에이션 테스트 (TDD).
/// 동영상, 음성 추출, 엑셀 표 데이터 등 킬러 포맷의 정상 노출 및 비정상 변환(png→docx 등) 차단을 검증한다.
/// </summary>
public class ContextMenuRegistrarTests
{
    private static ConversionEngine CreateEngine() => Everything2EverythingBootstrap.CreateDefault();

    [Fact]
    public void GetAvailableOutputs_Png_DoesNotContain_Docx()
    {
        var engine = CreateEngine();
        var outputs = ContextMenuRegistrar.GetAvailableOutputs(engine, ".png");

        Assert.DoesNotContain(outputs, o => o.Ext == ".docx");
        Assert.Contains(outputs, o => o.Ext == ".jpg");
        Assert.Contains(outputs, o => o.Ext == ".webp");
        Assert.Contains(outputs, o => o.Ext == ".pdf");
    }

    [Fact]
    public void GetAvailableOutputs_Video_Contains_Mp4_And_Mp3()
    {
        var engine = CreateEngine();
        var outputs = ContextMenuRegistrar.GetAvailableOutputs(engine, ".mkv");

        // 동영상 우클릭 메뉴에 MP4 변환 및 MP3 음원 추출이 노출되어야 한다.
        Assert.Contains(outputs, o => o.Ext == ".mp4");
        Assert.Contains(outputs, o => o.Ext == ".mp3");
    }

    [Fact]
    public void GetAvailableOutputs_Csv_Contains_Xlsx()
    {
        var engine = CreateEngine();
        var outputs = ContextMenuRegistrar.GetAvailableOutputs(engine, ".csv");

        // CSV 표 데이터 우클릭 메뉴에 Excel(.xlsx) 변환이 노출되어야 한다.
        Assert.Contains(outputs, o => o.Ext == ".xlsx");
    }

    [Fact]
    public void GetAvailableOutputs_Audio_DoesNotContain_Gif_Or_Video()
    {
        var engine = CreateEngine();
        var outputs = ContextMenuRegistrar.GetAvailableOutputs(engine, ".mp3");

        // 오디오 파일 우클릭 메뉴에 GIF나 비디오가 노출되지 않아야 한다.
        Assert.DoesNotContain(outputs, o => o.Ext == ".gif");
        Assert.DoesNotContain(outputs, o => o.Ext == ".mp4");
    }
}
