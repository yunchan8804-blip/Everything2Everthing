using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// FfmpegArgBuilder 인자 생성 단위 테스트 — ffmpeg 실행 없이(헤드리스) 옵션→ffmpeg 인자 매핑을 검증한다.
/// 레이트 컨트롤 5모드, 코덱 해소·컨테이너 호환, 프리셋·NVENC 매핑, -vf 필터 체인, 오디오 모드, faststart 등.
/// </summary>
public class FfmpegArgBuilderTests
{
    private static string Build(ConvertOptions o, string ext, EncoderTier tier = EncoderTier.Cpu, int width = 1920)
        => FfmpegArgBuilder.BuildOutputArguments(o, ext, tier, width);

    [Fact]
    public void Default_Mp4_Crf_Libx264_WithFaststart()
    {
        var args = Build(new ConvertOptions(), ".mp4");
        Assert.Contains("-c:v libx264", args);
        Assert.Contains("-crf 23", args);
        Assert.Contains("-preset medium", args);
        Assert.Contains("-pix_fmt yuv420p", args);
        Assert.Contains("-c:a aac", args);
        Assert.Contains("-b:a 192k", args);
        Assert.Contains("-movflags +faststart", args);
    }

    [Fact]
    public void Faststart_OnlyForMp4Mov_NotMkv()
    {
        Assert.Contains("+faststart", Build(new ConvertOptions(), ".mov"));
        Assert.DoesNotContain("+faststart", Build(new ConvertOptions(), ".mkv"));
        Assert.DoesNotContain("+faststart", Build(new ConvertOptions(), ".webm"));
    }

    [Fact]
    public void AutoCodec_HighRes_UsesH265()
    {
        var args = Build(new ConvertOptions(), ".mp4", EncoderTier.Cpu, width: 7680);
        Assert.Contains("-c:v libx265", args);
    }

    [Fact]
    public void Webm_ForcesVp9_AndOpus_WithB0ForCrf()
    {
        var args = Build(new ConvertOptions(), ".webm");
        Assert.Contains("-c:v libvpx-vp9", args);
        Assert.Contains("-b:v 0", args);          // vp9 CRF는 -b:v 0 동반 필수
        Assert.Contains("-c:a libopus", args);    // webm 오디오는 opus
    }

    [Fact]
    public void Nvenc_MapsCrfToCq_AndPresetToP4()
    {
        var args = Build(new ConvertOptions(), ".mp4", EncoderTier.Nvenc);
        Assert.Contains("-c:v h264_nvenc", args);
        Assert.Contains("-rc vbr", args);
        Assert.Contains("-cq 23", args);
        Assert.Contains("-preset p4", args);
        Assert.DoesNotContain("-crf", args);      // NVENC은 CRF 대신 CQ
    }

    [Fact]
    public void AverageBitrate_UsesBv()
    {
        var o = new ConvertOptions { Video = new VideoEncodeOptions { RateControl = RateControlMode.AverageBitrate, VideoBitrateKbps = 5000 } };
        var args = Build(o, ".mp4");
        Assert.Contains("-b:v 5000k", args);
        Assert.DoesNotContain("-crf", args);
    }

    [Fact]
    public void ConstrainedCrf_AddsMaxrateAndBufsize()
    {
        var o = new ConvertOptions { Video = new VideoEncodeOptions { RateControl = RateControlMode.ConstrainedCrf, Crf = 20, VideoMaxrateKbps = 8000 } };
        var args = Build(o, ".mp4");
        Assert.Contains("-crf 20", args);
        Assert.Contains("-maxrate 8000k", args);
        Assert.Contains("-bufsize 16000k", args);  // bufsize = 2x maxrate
    }

    [Fact]
    public void Cbr_AddsMinMaxBufsize()
    {
        var o = new ConvertOptions { Video = new VideoEncodeOptions { RateControl = RateControlMode.Cbr, VideoBitrateKbps = 4000 } };
        var args = Build(o, ".mp4");
        Assert.Contains("-minrate 4000k", args);
        Assert.Contains("-maxrate 4000k", args);
        Assert.Contains("-bufsize 8000k", args);
    }

    [Fact]
    public void FilterChain_OrdersCropDeinterlaceScaleTranspose()
    {
        var o = new ConvertOptions
        {
            Video = new VideoEncodeOptions
            {
                Crop = "640:480:0:0",
                Deinterlace = DeinterlaceMode.Yadif,
                ScaleWidth = 1280,
                DownscaleOnly = false,
                Rotate = RotateMode.Cw90,
            }
        };
        var args = Build(o, ".mp4");
        Assert.Contains("-vf \"crop=640:480:0:0,yadif,scale=1280:-2,transpose=1\"", args);
    }

    [Fact]
    public void Scale_DownscaleOnly_UsesMinExpression()
    {
        var o = new ConvertOptions { Video = new VideoEncodeOptions { ScaleWidth = 1280, DownscaleOnly = true } };
        Assert.Contains("scale='min(1280,iw)':-2", Build(o, ".mp4"));
    }

    [Fact]
    public void AudioOnly_Mp3_AddsVnAndLame()
    {
        var args = Build(new ConvertOptions(), ".mp3");
        Assert.Contains("-vn", args);                 // 비디오 스트림 제거
        Assert.Contains("-c:a libmp3lame", args);
        Assert.Contains("-b:a 192k", args);
        Assert.DoesNotContain("-c:v", args);
    }

    [Fact]
    public void Mp3_VbrMode_UsesQa_NotBa()
    {
        var o = new ConvertOptions { Audio = new AudioEncodeOptions { RateMode = AudioRateMode.Vbr, Mp3VbrQuality = 2 } };
        var args = Build(o, ".mp3");
        Assert.Contains("-q:a 2", args);
        Assert.DoesNotContain("-b:a", args);
    }

    [Fact]
    public void Flac_UsesCompressionLevel_NoBitrate()
    {
        var args = Build(new ConvertOptions(), ".flac");
        Assert.Contains("-c:a flac", args);
        Assert.Contains("-compression_level 5", args);
        Assert.DoesNotContain("-b:a", args);
    }

    [Fact]
    public void Wav_UsesPcmCodec_NoBitrate()
    {
        var args = Build(new ConvertOptions(), ".wav");
        Assert.Contains("-c:a pcm_s16le", args);
        Assert.DoesNotContain("-b:a", args);
    }

    [Fact]
    public void SampleRateChannels_AndLoudnorm()
    {
        var o = new ConvertOptions { Audio = new AudioEncodeOptions { SampleRate = 48000, Channels = 2, Loudnorm = true } };
        var args = Build(o, ".m4a");
        Assert.Contains("-ar 48000", args);
        Assert.Contains("-ac 2", args);
        Assert.Contains("-af loudnorm=I=-16:TP=-1.5:LRA=11", args);
    }

    [Fact]
    public void AudioCopy_PassthroughInVideo()
    {
        var o = new ConvertOptions { Audio = new AudioEncodeOptions { Copy = true } };
        var args = Build(o, ".mkv");
        Assert.Contains("-c:a copy", args);
    }

    [Fact]
    public void CopyVideoCodec_NoReencodeArgs()
    {
        var o = new ConvertOptions { Video = new VideoEncodeOptions { Codec = VideoCodec.Copy } };
        var args = Build(o, ".mp4");
        Assert.Contains("-c:v copy", args);
        Assert.DoesNotContain("-crf", args);
        Assert.DoesNotContain("-preset", args);
    }

    [Theory]
    [InlineData(VideoCodec.Auto, ".mp4", 1920, VideoCodec.H264)]
    [InlineData(VideoCodec.Auto, ".mp4", 7680, VideoCodec.H265)]
    [InlineData(VideoCodec.H264, ".webm", 1920, VideoCodec.Vp9)] // webm은 h264 불가 → vp9 강제
    [InlineData(VideoCodec.Av1, ".webm", 1920, VideoCodec.Av1)]
    [InlineData(VideoCodec.H265, ".mp4", 1920, VideoCodec.H265)]
    public void ResolveVideoCodec_EnforcesContainerCompat(VideoCodec requested, string ext, int width, VideoCodec expected)
    {
        Assert.Equal(expected, FfmpegArgBuilder.ResolveVideoCodec(requested, ext, width));
    }

    [Fact]
    public void GpuApplicable_OnlyForH26xMp4Family_8bit()
    {
        var v = new VideoEncodeOptions();
        Assert.True(FfmpegArgBuilder.GpuApplicable(v, VideoCodec.H264, ".mp4"));
        Assert.True(FfmpegArgBuilder.GpuApplicable(v, VideoCodec.H265, ".mkv"));
        Assert.False(FfmpegArgBuilder.GpuApplicable(v, VideoCodec.Vp9, ".webm"));   // vp9 HW 미적용
        Assert.False(FfmpegArgBuilder.GpuApplicable(v, VideoCodec.H264, ".avi"));   // avi 미적용
        Assert.False(FfmpegArgBuilder.GpuApplicable(v with { PixelFormat = "yuv420p10le" }, VideoCodec.H265, ".mp4")); // 10bit CPU 선호
    }
}
