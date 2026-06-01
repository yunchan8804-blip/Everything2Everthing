using System.Linq;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Everything2Everything.Core.Providers;
using Xunit;

namespace Everything2Everything.Tests;

public class FfmpegProviderTests
{
    [Fact]
    public async Task WithoutFfmpeg_IsNotReady()
    {
        // 테스트 환경에 ffmpeg가 없다고 가정. 있으면 이 단언은 건너뜀.
        if (ExternalToolDetector.TryFindFfmpeg(out _)) return;
        var p = new FfmpegProvider();
        var a = await p.CheckAvailabilityAsync();
        Assert.False(a.IsReady);
        Assert.NotNull(a.Reason);
    }

    [Fact]
    public void Graph_HasVideoAndAudioEdges()
    {
        var graph = Everything2EverythingBootstrap.CreateDefault().Providers.Graph;
        Assert.NotNull(graph.FindBestPath(".mp4", ".webm")); // 영상 트랜스코딩
        Assert.NotNull(graph.FindBestPath(".wav", ".mp3"));  // 오디오 변환
        Assert.NotNull(graph.FindBestPath(".mp4", ".mp3"));  // 영상→오디오 추출
        Assert.NotNull(graph.FindBestPath(".mp4", ".mp4"));  // 동일 포맷 재인코딩(압축) self-edge
        Assert.NotNull(graph.FindBestPath(".mp3", ".mp3"));  // 동일 포맷 오디오 재인코딩 self-edge
    }

    [Fact]
    public void Capability_DeclaresVideoAudioMatrix_WithReEncodeSelfPairs()
    {
        var caps = new FfmpegProvider().Capability.SupportedConversions;
        Assert.Contains(caps, c => c.InputExtension == ".mp4" && c.OutputExtension == ".mkv");
        Assert.Contains(caps, c => c.InputExtension == ".flac" && c.OutputExtension == ".mp3");
        // 동일 포맷 재인코딩(압축) self-edge: 비디오 컨테이너·오디오는 self-pair를 허용한다.
        Assert.Contains(caps, c => c.InputExtension == ".mp4" && c.OutputExtension == ".mp4");
        Assert.Contains(caps, c => c.InputExtension == ".wav" && c.OutputExtension == ".wav");
        // gif는 self-edge에서 제외 — 이미지로 다뤄지며 ffmpeg 미설치 시 회귀를 막기 위함.
        Assert.DoesNotContain(caps, c => c.InputExtension == ".gif" && c.OutputExtension == ".gif");
    }
}
