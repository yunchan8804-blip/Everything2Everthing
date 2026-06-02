using System.Globalization;
using System.Text;
using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Converters;

/// <summary>하드웨어 인코더 계층 — NVENC→QSV→AMF→CPU 순으로 폴백.</summary>
public enum EncoderTier { Nvenc, Qsv, Amf, Cpu }

/// <summary>
/// VideoEncodeOptions/AudioEncodeOptions를 실제 ffmpeg 출력 인자 문자열로 변환한다.
/// WPF/FFMpegCore 비의존 순수 함수 모음이라 헤드리스 단위 테스트가 가능하다(인자 생성 검증).
/// 인코더 계층(EncoderTier)별로 동일 옵션을 다른 인자로 매핑한다(소프트웨어 CRF/preset ↔ NVENC -cq/-preset p1~p7 등).
/// </summary>
public static class FfmpegArgBuilder
{
    private static string Ci(double v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>요청 코덱 + 컨테이너 호환 + 자동선택(고해상도 HEVC)을 반영해 실제 사용할 코덱을 해소.</summary>
    public static VideoCodec ResolveVideoCodec(VideoCodec requested, string outExt, int sourceWidth)
    {
        outExt = ConversionPair.Normalize(outExt);
        if (requested == VideoCodec.Copy) return VideoCodec.Copy;

        // webm은 vp9/av1만 허용 — 잘못된 조합을 원천 차단
        if (outExt == ".webm")
            return requested is VideoCodec.Av1 ? VideoCodec.Av1 : VideoCodec.Vp9;

        if (requested != VideoCodec.Auto) return requested;

        // Auto: 8K 등 4096px 초과는 H.264가 인코딩 못 하므로 HEVC로(현행 동작 보존)
        return sourceWidth > 4096 ? VideoCodec.H265 : VideoCodec.H264;
    }

    /// <summary>해소된 코덱 + 계층 → ffmpeg 인코더 이름.</summary>
    public static string VideoEncoderName(VideoCodec codec, EncoderTier tier) => codec switch
    {
        VideoCodec.Copy => "copy",
        VideoCodec.Vp9 => "libvpx-vp9",
        VideoCodec.Av1 => "libsvtav1",
        VideoCodec.H264 => tier switch
        {
            EncoderTier.Nvenc => "h264_nvenc",
            EncoderTier.Qsv => "h264_qsv",
            EncoderTier.Amf => "h264_amf",
            _ => "libx264",
        },
        VideoCodec.H265 => tier switch
        {
            EncoderTier.Nvenc => "hevc_nvenc",
            EncoderTier.Qsv => "hevc_qsv",
            EncoderTier.Amf => "hevc_amf",
            _ => "libx265",
        },
        _ => "libx264",
    };

    /// <summary>GPU(HW 인코더) 시도가 가능한 조합인가. h264/h265 + mp4/mkv/mov + 8bit 4:2:0만.</summary>
    public static bool GpuApplicable(VideoEncodeOptions v, VideoCodec resolved, string outExt)
    {
        outExt = ConversionPair.Normalize(outExt);
        if (resolved is not (VideoCodec.H264 or VideoCodec.H265)) return false;
        if (outExt is not (".mp4" or ".mkv" or ".mov")) return false;
        if (v.Lossless) return false;
        // 10bit/4:4:4는 HW 인코더 지원이 제한적 — CPU 경로 선호
        if (!string.IsNullOrEmpty(v.PixelFormat) && (v.PixelFormat.Contains("10le") || v.PixelFormat.Contains("444"))) return false;
        return true;
    }

    /// <summary>오디오 코덱(Auto면 출력 확장자/컨테이너에서 매핑) → ffmpeg 인코더 이름.</summary>
    public static string ResolveAudioEncoder(AudioCodec codec, string outExt)
    {
        outExt = ConversionPair.Normalize(outExt);
        if (codec == AudioCodec.Copy) return "copy";
        if (codec != AudioCodec.Auto)
            return codec switch
            {
                AudioCodec.Aac => "aac",
                AudioCodec.Mp3 => "libmp3lame",
                AudioCodec.Opus => "libopus",
                AudioCodec.Vorbis => "libvorbis",
                AudioCodec.Flac => "flac",
                AudioCodec.Pcm => "pcm_s16le",
                _ => "aac",
            };

        // Auto: 오디오 전용 컨테이너는 확장자로, 영상 컨테이너는 webm→opus / 그 외 aac
        return outExt switch
        {
            ".mp3" => "libmp3lame",
            ".aac" or ".m4a" => "aac",
            ".opus" => "libopus",
            ".ogg" => "libvorbis",
            ".flac" => "flac",
            ".wav" => "pcm_s16le",
            ".webm" => "libopus",
            _ => "aac",
        };
    }

    public static bool IsAudioContainer(string outExt)
        => ConversionPair.Normalize(outExt) is ".mp3" or ".aac" or ".m4a" or ".opus" or ".ogg" or ".flac" or ".wav";

    /// <summary>출력 인자 전체(-c:v … -vf … -c:a … -movflags …)를 한 문자열로 구성.</summary>
    public static string BuildOutputArguments(ConvertOptions options, string outExt, EncoderTier tier, int sourceWidth)
    {
        outExt = ConversionPair.Normalize(outExt);
        var args = new List<string>();

        if (IsAudioContainer(outExt))
        {
            args.Add("-vn"); // 비디오 스트림 제거(오디오 전용 출력)
            AddAudioArgs(args, options.Audio, outExt);
            return Join(args);
        }

        var v = options.Video;
        var codec = ResolveVideoCodec(v.Codec, outExt, sourceWidth);
        args.Add($"-c:v {VideoEncoderName(codec, tier)}");

        if (codec != VideoCodec.Copy)
        {
            AddRateControl(args, v, codec, tier);
            args.Add(PresetArg(v.Preset, codec, tier));

            if (!string.IsNullOrWhiteSpace(v.Tune)) args.Add(TuneArg(v.Tune!, codec));
            if (!string.IsNullOrWhiteSpace(v.Profile)) args.Add($"-profile:v {v.Profile}");
            if (!string.IsNullOrWhiteSpace(v.Level)) args.Add($"-level {v.Level}");
            if (!string.IsNullOrWhiteSpace(v.PixelFormat)) args.Add($"-pix_fmt {v.PixelFormat}");
            if (v.GopSize is int g and > 0) args.Add($"-g {g}");
            if (v.BFrames is int bf and >= 0 && tier == EncoderTier.Cpu) args.Add($"-bf {bf}");
            if (tier != EncoderTier.Cpu && v.SpatialAq) AddSpatialAq(args, tier);

            var vf = BuildVideoFilterChain(v);
            if (vf.Length > 0) args.Add($"-vf \"{vf}\"");
            if (v.Fps is double fps and > 0) args.Add($"-r {Ci(fps)}");

            if (!string.IsNullOrWhiteSpace(v.RawArgs)) args.Add(v.RawArgs!.Trim());
        }

        // 오디오 트랙
        AddAudioArgs(args, options.Audio, outExt);

        // faststart (mp4/mov만)
        if (v.FastStart && outExt is ".mp4" or ".mov") args.Add("-movflags +faststart");

        return Join(args);
    }

    // ── 레이트 컨트롤 ────────────────────────────────────────────────────────────────────
    private static void AddRateControl(List<string> args, VideoEncodeOptions v, VideoCodec codec, EncoderTier tier)
    {
        if (v.Lossless)
        {
            if (codec == VideoCodec.H264 && tier == EncoderTier.Cpu) args.Add("-crf 0");
            else if (codec == VideoCodec.H265 && tier == EncoderTier.Cpu) args.Add("-x265-params lossless=1");
            else args.Add("-cq 0"); // HW 근사
            return;
        }

        var b = v.VideoBitrateKbps ?? 0;
        var max = v.VideoMaxrateKbps ?? 0;
        var needsB0 = codec is VideoCodec.Vp9 or VideoCodec.Av1; // CRF 활성 신호

        switch (v.RateControl)
        {
            case RateControlMode.Crf:
                if (tier == EncoderTier.Cpu)
                {
                    args.Add($"-crf {v.Crf}");
                    if (needsB0) args.Add("-b:v 0");
                }
                else if (tier == EncoderTier.Nvenc) { args.Add("-rc vbr"); args.Add($"-cq {v.Crf}"); }
                else if (tier == EncoderTier.Qsv) { args.Add($"-global_quality {v.Crf}"); }
                else { args.Add("-rc qvbr"); args.Add($"-qvbr_quality_level {v.Crf}"); } // AMF
                break;

            case RateControlMode.AverageBitrate:
            case RateControlMode.TwoPass: // 2패스는 provider가 -pass 1/2를 덧붙임
                if (tier == EncoderTier.Nvenc) args.Add("-rc vbr");
                args.Add($"-b:v {b}k");
                break;

            case RateControlMode.ConstrainedCrf:
                if (tier == EncoderTier.Cpu)
                {
                    args.Add($"-crf {v.Crf}");
                    if (needsB0) args.Add("-b:v 0");
                }
                else if (tier == EncoderTier.Nvenc) { args.Add("-rc vbr"); args.Add($"-cq {v.Crf}"); }
                else if (tier == EncoderTier.Qsv) { args.Add($"-global_quality {v.Crf}"); }
                else { args.Add("-rc qvbr"); args.Add($"-qvbr_quality_level {v.Crf}"); }
                if (max > 0) { args.Add($"-maxrate {max}k"); args.Add($"-bufsize {max * 2}k"); }
                break;

            case RateControlMode.Cbr:
                if (tier == EncoderTier.Nvenc) { args.Add("-rc cbr"); args.Add($"-b:v {b}k"); }
                else { args.Add($"-b:v {b}k"); args.Add($"-minrate {b}k"); args.Add($"-maxrate {b}k"); args.Add($"-bufsize {b * 2}k"); }
                break;
        }
    }

    // ── 프리셋 ──────────────────────────────────────────────────────────────────────────
    private static string PresetArg(VideoSpeedPreset preset, VideoCodec codec, EncoderTier tier)
    {
        if (tier == EncoderTier.Nvenc) return $"-preset {NvencPreset(preset)} -tune hq";
        if (tier == EncoderTier.Amf) return $"-quality {AmfQuality(preset)}";
        if (tier == EncoderTier.Qsv) return $"-preset {X26xPreset(preset)}";

        // CPU
        return codec switch
        {
            VideoCodec.Av1 => $"-preset {SvtAv1Preset(preset)}",
            VideoCodec.Vp9 => $"-deadline good -cpu-used {Vp9CpuUsed(preset)}",
            _ => $"-preset {X26xPreset(preset)}",
        };
    }

    private static string X26xPreset(VideoSpeedPreset p) => p switch
    {
        VideoSpeedPreset.UltraFast => "ultrafast",
        VideoSpeedPreset.SuperFast => "superfast",
        VideoSpeedPreset.VeryFast => "veryfast",
        VideoSpeedPreset.Faster => "faster",
        VideoSpeedPreset.Fast => "fast",
        VideoSpeedPreset.Medium => "medium",
        VideoSpeedPreset.Slow => "slow",
        VideoSpeedPreset.Slower => "slower",
        VideoSpeedPreset.VerySlow => "veryslow",
        _ => "medium",
    };

    // libsvtav1: 낮을수록 느림·고품질
    private static int SvtAv1Preset(VideoSpeedPreset p) => p switch
    {
        VideoSpeedPreset.UltraFast => 12,
        VideoSpeedPreset.SuperFast => 11,
        VideoSpeedPreset.VeryFast => 10,
        VideoSpeedPreset.Faster => 9,
        VideoSpeedPreset.Fast => 8,
        VideoSpeedPreset.Medium => 7,
        VideoSpeedPreset.Slow => 5,
        VideoSpeedPreset.Slower => 4,
        VideoSpeedPreset.VerySlow => 2,
        _ => 7,
    };

    // vp9 -cpu-used: 낮을수록 느림·고품질 (0~8)
    private static int Vp9CpuUsed(VideoSpeedPreset p) => p switch
    {
        VideoSpeedPreset.UltraFast => 8,
        VideoSpeedPreset.SuperFast => 6,
        VideoSpeedPreset.VeryFast => 5,
        VideoSpeedPreset.Faster => 4,
        VideoSpeedPreset.Fast => 3,
        VideoSpeedPreset.Medium => 2,
        VideoSpeedPreset.Slow => 1,
        VideoSpeedPreset.Slower => 1,
        VideoSpeedPreset.VerySlow => 0,
        _ => 2,
    };

    // NVENC p1(가장 빠름)~p7(가장 느림·고품질)
    private static string NvencPreset(VideoSpeedPreset p) => p switch
    {
        VideoSpeedPreset.UltraFast => "p1",
        VideoSpeedPreset.SuperFast => "p1",
        VideoSpeedPreset.VeryFast => "p2",
        VideoSpeedPreset.Faster => "p3",
        VideoSpeedPreset.Fast => "p3",
        VideoSpeedPreset.Medium => "p4",
        VideoSpeedPreset.Slow => "p5",
        VideoSpeedPreset.Slower => "p6",
        VideoSpeedPreset.VerySlow => "p7",
        _ => "p4",
    };

    private static string AmfQuality(VideoSpeedPreset p) => p switch
    {
        VideoSpeedPreset.UltraFast or VideoSpeedPreset.SuperFast or VideoSpeedPreset.VeryFast => "speed",
        VideoSpeedPreset.Slow or VideoSpeedPreset.Slower or VideoSpeedPreset.VerySlow => "quality",
        _ => "balanced",
    };

    private static string TuneArg(string tune, VideoCodec codec)
        => codec == VideoCodec.Av1 ? $"-svtav1-params tune={tune}" : $"-tune {tune}";

    private static void AddSpatialAq(List<string> args, EncoderTier tier)
    {
        switch (tier)
        {
            case EncoderTier.Nvenc: args.Add("-spatial-aq 1"); args.Add("-aq-strength 8"); args.Add("-rc-lookahead 20"); break;
            case EncoderTier.Amf: args.Add("-vbaq 1"); break;
            case EncoderTier.Qsv: args.Add("-extbrc 1"); break;
        }
    }

    // ── 영상 필터 체인 (-vf) ────────────────────────────────────────────────────────────
    /// <summary>crop → deinterlace → denoise → scale → transpose/flip 순으로 콤마 체인 구성.</summary>
    public static string BuildVideoFilterChain(VideoEncodeOptions v)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(v.Crop)) parts.Add($"crop={v.Crop}");

        switch (v.Deinterlace)
        {
            case DeinterlaceMode.Yadif: parts.Add("yadif"); break;
            case DeinterlaceMode.Bwdif: parts.Add("bwdif=mode=send_frame:parity=auto"); break;
        }

        if (!string.IsNullOrWhiteSpace(v.Denoise)) parts.Add(v.Denoise!);

        if (v.ScaleWidth is int w and > 0)
            parts.Add(v.DownscaleOnly ? $"scale='min({w},iw)':-2" : $"scale={w}:-2");

        switch (v.Rotate)
        {
            case RotateMode.Cw90: parts.Add("transpose=1"); break;
            case RotateMode.Ccw90: parts.Add("transpose=2"); break;
            case RotateMode.Rotate180: parts.Add("transpose=1,transpose=1"); break;
            case RotateMode.FlipH: parts.Add("hflip"); break;
            case RotateMode.FlipV: parts.Add("vflip"); break;
        }

        return string.Join(",", parts);
    }

    // ── 오디오 인자 ─────────────────────────────────────────────────────────────────────
    private static void AddAudioArgs(List<string> args, AudioEncodeOptions a, string outExt)
    {
        if (a.Copy || a.Codec == AudioCodec.Copy) { args.Add("-c:a copy"); return; }

        var encoder = ResolveAudioEncoder(a.Codec, outExt);
        args.Add($"-c:a {encoder}");

        if (encoder.StartsWith("pcm_"))
        {
            // WAV: PcmFormat이 코덱 자체 — 비트레이트/품질 개념 없음
        }
        else if (encoder == "flac")
        {
            args.Add($"-compression_level {Math.Clamp(a.FlacCompressionLevel, 0, 12)}");
        }
        else if (a.RateMode == AudioRateMode.Vbr)
        {
            switch (encoder)
            {
                case "libmp3lame": args.Add($"-q:a {Math.Clamp(a.Mp3VbrQuality, 0, 9)}"); break;
                case "libvorbis": args.Add($"-q:a {Ci(Math.Clamp(a.VorbisVbrQuality, -1, 10))}"); break;
                case "libopus": args.Add("-vbr on"); args.Add($"-b:a {a.AudioBitrateKbps}k"); break;
                default: args.Add($"-b:a {a.AudioBitrateKbps}k"); break; // aac native VBR은 experimental → CBR
            }
        }
        else
        {
            args.Add($"-b:a {a.AudioBitrateKbps}k");
        }

        if (a.SampleRate is int sr and > 0) args.Add($"-ar {sr}");
        if (a.Channels is int ch and > 0) args.Add($"-ac {ch}");

        // 오디오 필터(-af) — Loudnorm 우선, 아니면 VolumeGain
        if (a.Loudnorm) args.Add("-af loudnorm=I=-16:TP=-1.5:LRA=11");
        else if (a.VolumeGain is double vol && Math.Abs(vol - 1.0) > 0.001) args.Add($"-af volume={Ci(vol)}");
    }

    private static string Join(List<string> args)
    {
        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(a.Trim());
        }
        return sb.ToString();
    }
}
