using CommunityToolkit.Mvvm.ComponentModel;
using Everything2Everything.Core;

namespace Everything2Everything.App.ViewModels;

/// <summary>
/// 변환 옵션 패널의 상태 + 불변 ConvertOptions 구성 로직(MVVM ViewModel).
/// ToConvertOptions는 순수 함수라 WPF 없이 헤드리스 단위 테스트가 가능하다(P5b: 코드비하인드 BuildOptions 추출).
/// ObservableProperty로 노출되어 향후 옵션 패널 XAML을 이 VM에 직접 바인딩할 수 있다.
/// </summary>
public partial class OptionsViewModel : ObservableObject
{
    /// <summary>JPEG/WebP 품질(1~100). AVIF는 -30 보정.</summary>
    [ObservableProperty] private int _quality = 85;

    /// <summary>비우면 원본 옆 서브폴더. 값이 있으면 사용자 지정 출력 폴더.</summary>
    [ObservableProperty] private string? _customOutputDirectory;

    [ObservableProperty] private NameCollision _conflictRule = NameCollision.AppendNumber;

    /// <summary>0=요약, 1=번역, 2=교정 (AI 텍스트 변환).</summary>
    [ObservableProperty] private int _aiTaskIndex;

    [ObservableProperty] private string? _targetLanguage;

    [ObservableProperty] private bool _videoPreferGpu = true;

    // ── 영상 인코딩 (출력이 영상 컨테이너일 때) ──────────────────────────────────────────────
    [ObservableProperty] private int _videoCodecIndex;       // 0=Auto,1=H264,2=H265,3=VP9,4=AV1,5=Copy
    [ObservableProperty] private int _rateControlIndex;      // 0=CRF,1=ABR,2=ConstrainedCRF,3=CBR,4=2pass
    [ObservableProperty] private int _crf = 23;              // 0~51
    [ObservableProperty] private int _videoBitrateKbps = 8000;
    [ObservableProperty] private int _presetIndex = 5;       // 0=UltraFast..8=VerySlow(기본 Medium)
    [ObservableProperty] private int _resolutionIndex;       // 0=원본,1=2160p,2=1440p,3=1080p,4=720p,5=480p
    [ObservableProperty] private int _fpsIndex;              // 0=원본,1=24,2=30,3=60
    [ObservableProperty] private int _rotateIndex;           // 0=None,1=Cw90,2=Ccw90,3=180,4=FlipH,5=FlipV
    [ObservableProperty] private int _deinterlaceIndex;      // 0=Off,1=Yadif,2=Bwdif
    [ObservableProperty] private bool _fastStart = true;
    [ObservableProperty] private bool _spatialAq = true;

    // ── 오디오 인코딩 (영상의 오디오 트랙 + 오디오 전용 출력) ─────────────────────────────────
    [ObservableProperty] private int _audioCodecIndex;       // 0=Auto,1=AAC,2=MP3,3=Opus,4=Vorbis,5=FLAC,6=PCM,7=Copy
    [ObservableProperty] private int _audioBitrateIndex = 2; // 0=96,1=128,2=192,3=256,4=320
    [ObservableProperty] private bool _audioVbr;
    [ObservableProperty] private int _sampleRateIndex;       // 0=원본,1=44100,2=48000
    [ObservableProperty] private int _channelsIndex;         // 0=원본,1=모노,2=스테레오
    [ObservableProperty] private bool _loudnorm;

    /// <summary>현재 상태로 불변 ConvertOptions를 구성한다(기존 MainWindow.BuildOptions와 동일 동작 + 영상/오디오).</summary>
    public ConvertOptions ToConvertOptions()
    {
        var hasCustom = !string.IsNullOrWhiteSpace(CustomOutputDirectory);
        var aiTask = AiTaskIndex switch
        {
            1 => "translate",
            2 => "proofread",
            _ => "summarize",
        };

        return new ConvertOptions
        {
            OnCollision = ConflictRule,
            OutputLocation = hasCustom ? OutputLocation.Custom : OutputLocation.SubfolderBesideSource,
            CustomOutputDirectory = hasCustom ? CustomOutputDirectory!.Trim() : null,
            Jpeg = new JpegEncodingOptions { Quality = Quality },
            Webp = new WebpEncodingOptions { Quality = Quality },
            Avif = new AvifEncodingOptions { Quality = Math.Clamp(Quality - 30, 1, 100) },
            Ai = new AiOptions
            {
                Task = aiTask,
                TargetLanguage = string.IsNullOrWhiteSpace(TargetLanguage) ? null : TargetLanguage.Trim(),
            },
            VideoPreferGpu = VideoPreferGpu,
            Video = BuildVideo(),
            Audio = BuildAudio(),
        };
    }

    private VideoEncodeOptions BuildVideo() => new()
    {
        Codec = VideoCodecIndex switch { 1 => VideoCodec.H264, 2 => VideoCodec.H265, 3 => VideoCodec.Vp9, 4 => VideoCodec.Av1, 5 => VideoCodec.Copy, _ => VideoCodec.Auto },
        RateControl = RateControlIndex switch { 1 => RateControlMode.AverageBitrate, 2 => RateControlMode.ConstrainedCrf, 3 => RateControlMode.Cbr, 4 => RateControlMode.TwoPass, _ => RateControlMode.Crf },
        Crf = Math.Clamp(Crf, 0, 63),
        VideoBitrateKbps = VideoBitrateKbps,
        VideoMaxrateKbps = RateControlIndex == 2 ? VideoBitrateKbps : null, // 제약CRF는 비트레이트 입력을 상한으로
        Preset = (VideoSpeedPreset)Math.Clamp(PresetIndex, 0, 8),
        ScaleWidth = ResolutionIndex switch { 1 => 3840, 2 => 2560, 3 => 1920, 4 => 1280, 5 => 854, _ => (int?)null },
        Fps = FpsIndex switch { 1 => 24.0, 2 => 30.0, 3 => 60.0, _ => (double?)null },
        Rotate = (RotateMode)Math.Clamp(RotateIndex, 0, 5),
        Deinterlace = (DeinterlaceMode)Math.Clamp(DeinterlaceIndex, 0, 2),
        FastStart = FastStart,
        SpatialAq = SpatialAq,
    };

    private AudioEncodeOptions BuildAudio() => new()
    {
        Codec = AudioCodecIndex switch { 1 => AudioCodec.Aac, 2 => AudioCodec.Mp3, 3 => AudioCodec.Opus, 4 => AudioCodec.Vorbis, 5 => AudioCodec.Flac, 6 => AudioCodec.Pcm, 7 => AudioCodec.Copy, _ => AudioCodec.Auto },
        RateMode = AudioVbr ? AudioRateMode.Vbr : AudioRateMode.Bitrate,
        AudioBitrateKbps = AudioBitrateIndex switch { 0 => 96, 1 => 128, 3 => 256, 4 => 320, _ => 192 },
        SampleRate = SampleRateIndex switch { 1 => 44100, 2 => 48000, _ => (int?)null },
        Channels = ChannelsIndex switch { 1 => 1, 2 => 2, _ => (int?)null },
        Loudnorm = Loudnorm,
    };
}
