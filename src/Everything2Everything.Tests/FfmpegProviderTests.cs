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
    }

    [Fact]
    public void Capability_DeclaresVideoAudioMatrix_NoSelfPairs()
    {
        var caps = new FfmpegProvider().Capability.SupportedConversions;
        Assert.Contains(caps, c => c.InputExtension == ".mp4" && c.OutputExtension == ".mkv");
        Assert.Contains(caps, c => c.InputExtension == ".flac" && c.OutputExtension == ".mp3");
        Assert.DoesNotContain(caps, c => c.InputExtension == c.OutputExtension); // 자기쌍 없음
    }
}
