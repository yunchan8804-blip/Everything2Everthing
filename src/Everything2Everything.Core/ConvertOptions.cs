namespace Everything2Everything.Core;

public enum OutputLocation
{
    SubfolderBesideSource,
    SameFolderAsSource,
    Custom
}

public enum NameCollision
{
    AppendNumber,
    Overwrite,
    Skip
}

/// <summary>
/// 변환 옵션. 불변(record + init-only) — 구성된 뒤에는 변경되지 않으므로 배치 병렬 변환에서 안전하게 공유된다.
/// 변경이 필요하면 with 식으로 새 인스턴스를 만든다. (P4: mutable God Object → immutable record)
/// </summary>
public sealed record ConvertOptions
{
    public OutputLocation OutputLocation { get; init; } = OutputLocation.SubfolderBesideSource;

    public string SubfolderSuffix { get; init; } = "_converted";

    public string? CustomOutputDirectory { get; init; }

    public NameCollision OnCollision { get; init; } = NameCollision.AppendNumber;

    public int? MaxLongEdgePixels { get; init; }

    public bool KeepExifWhenPossible { get; init; } = true;

    public bool FlattenTransparency { get; init; } = false;

    public string TransparencyBackground { get; init; } = "#FFFFFF";

    public JpegEncodingOptions Jpeg { get; init; } = new();

    public PngEncodingOptions Png { get; init; } = new();

    public WebpEncodingOptions Webp { get; init; } = new();

    public AvifEncodingOptions Avif { get; init; } = new();

    public TiffEncodingOptions Tiff { get; init; } = new();

    public BmpEncodingOptions Bmp { get; init; } = new();

    public GifEncodingOptions Gif { get; init; } = new();

    public PdfRenderOptions PdfRender { get; init; } = new();

    public PdfBuildOptions PdfBuild { get; init; } = new();

    public HtmlRenderOptions HtmlRender { get; init; } = new();

    public OcrOptions Ocr { get; init; } = new();

    // --- 변환 그래프 경로 옵션 (P1) ---
    /// <summary>멀티홉 경로 자동 합성 허용. false면 직접(1홉) 변환만.</summary>
    public bool AllowMultiHop { get; init; } = true;

    /// <summary>멀티홉 최대 홉 수.</summary>
    public int MaxHops { get; init; } = 3;

    /// <summary>래스터화 같은 큰 손실 엣지를 회피한다.</summary>
    public bool AvoidLossy { get; init; } = false;

    /// <summary>독립(Independent) 배치 변환의 최대 병렬 수. 기본 = 논리 코어 수. 미디어(FFmpeg) 위주 배치는 낮춰 오버서브스크립션 회피.</summary>
    public int BatchParallelism { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// LibreOffice(soffice) 변환 1건의 타임아웃(초). 초과 시 프로세스 트리를 강제 종료해 hang을 회수한다.
    /// 특정 HWP/문서에서 soffice가 무한 대기하는 사례를 방지(기본 120초). 큰 문서가 많으면 늘린다.
    /// </summary>
    public int LibreOfficeTimeoutSeconds { get; init; } = 120;

    /// <summary>영상 인코딩 시 GPU 하드웨어 가속(NVENC)을 우선 시도하고, 실패하면 CPU로 자동 폴백한다.</summary>
    public bool VideoPreferGpu { get; init; } = true;

    public PdfCompressOptions PdfCompress { get; init; } = new();

    public AiOptions Ai { get; init; } = new();

    /// <summary>영상 인코딩 상세 옵션(FfmpegProvider). 출력이 영상 컨테이너일 때 적용.</summary>
    public VideoEncodeOptions Video { get; init; } = new();

    /// <summary>오디오 인코딩 상세 옵션(FfmpegProvider). 영상의 오디오 트랙 + 오디오 전용 출력에 적용.</summary>
    public AudioEncodeOptions Audio { get; init; } = new();

    public static ConvertOptions Quick() => new();
}

public sealed record JpegEncodingOptions
{
    public int Quality { get; init; } = 92;
    public bool Progressive { get; init; } = false;
}

public sealed record PngEncodingOptions
{
    public int Compression { get; init; } = 7;
    public bool Interlace { get; init; } = false;
}

public sealed record WebpEncodingOptions
{
    public int Quality { get; init; } = 90;
    public bool Lossless { get; init; } = false;
}

public sealed record AvifEncodingOptions
{
    public int Quality { get; init; } = 60;
    public int Speed { get; init; } = 6;
}

public sealed record TiffEncodingOptions
{
    public string Compression { get; init; } = "lzw";
}

public sealed record BmpEncodingOptions
{
}

public sealed record GifEncodingOptions
{
}

public sealed record PdfRenderOptions
{
    public int Dpi { get; init; } = 200;
    public bool WithAnnotations { get; init; } = true;
    public bool WithFormFill { get; init; } = true;
}

public sealed record PdfBuildOptions
{
    public string PageSize { get; init; } = "Auto";
    public int MarginPoints { get; init; } = 24;
    public bool FitToPage { get; init; } = true;
}

public sealed record HtmlRenderOptions
{
    public int ViewportWidth { get; init; } = 1280;
    public int? ViewportHeight { get; init; }
    public int WaitMilliseconds { get; init; } = 2000;
    public bool FullPage { get; init; } = true;
}

public sealed record OcrOptions
{
    public string Language { get; init; } = "ko+en";
    public bool PreserveLayout { get; init; } = true;
    public string Backend { get; init; } = "auto";
}

public sealed record PdfCompressOptions
{
    /// <summary>Light(구조 최적화·무손실) | Strong(렌더 재인코딩) | Max(Ghostscript). P1은 Light만 구현.</summary>
    public string Level { get; init; } = "Light";
}

public sealed record AiOptions
{
    /// <summary>auto | openai | anthropic. auto는 설정된 키 중 가용한 것을 선택.</summary>
    public string Backend { get; init; } = "auto";

    /// <summary>모델 ID. null이면 백엔드별 기본값.</summary>
    public string? Model { get; init; }

    /// <summary>summarize | translate | proofread | custom.</summary>
    public string Task { get; init; } = "summarize";

    /// <summary>translate 작업의 대상 언어 (예: "영어", "일본어").</summary>
    public string? TargetLanguage { get; init; }

    /// <summary>custom 작업의 사용자 지정 지시문.</summary>
    public string? Instruction { get; init; }

    public int MaxOutputTokens { get; init; } = 2000;
}

// ── 영상·오디오 인코딩 옵션 (FfmpegProvider) ──────────────────────────────────────────────

public enum VideoCodec { Auto, H264, H265, Vp9, Av1, Copy }
public enum AudioCodec { Auto, Aac, Mp3, Opus, Vorbis, Flac, Pcm, Copy }

/// <summary>레이트 컨트롤(품질/용량 통제) 모드.</summary>
public enum RateControlMode
{
    /// <summary>상수 품질(CRF) — 품질만 정하고 용량은 가변. 가장 일반적.</summary>
    Crf,
    /// <summary>평균 비트레이트(ABR) — 목표 비트레이트 1패스.</summary>
    AverageBitrate,
    /// <summary>제약된 CRF — CRF 품질 + 최대 비트레이트 상한(스트리밍).</summary>
    ConstrainedCrf,
    /// <summary>고정 비트레이트(CBR) — 방송/스트리밍.</summary>
    Cbr,
    /// <summary>2패스 — 목표 비트레이트에서 최고 품질(2회 인코딩).</summary>
    TwoPass,
}

public enum AudioRateMode { Bitrate, Vbr }

public enum VideoSpeedPreset { UltraFast, SuperFast, VeryFast, Faster, Fast, Medium, Slow, Slower, VerySlow }

public enum RotateMode { None, Cw90, Ccw90, Rotate180, FlipH, FlipV }

public enum DeinterlaceMode { Off, Yadif, Bwdif }

/// <summary>영상 인코딩 상세 옵션. 모두 불변(get;init). 출력이 영상 컨테이너(mp4/mkv/webm/mov/avi)일 때만 적용.</summary>
public sealed record VideoEncodeOptions
{
    public VideoCodec Codec { get; init; } = VideoCodec.Auto;
    public RateControlMode RateControl { get; init; } = RateControlMode.Crf;

    /// <summary>CRF 값(낮을수록 고화질). x264/x265 0~51, vp9/av1 0~63.</summary>
    public int Crf { get; init; } = 23;

    /// <summary>비트레이트 모드의 목표 비디오 비트레이트(kbps).</summary>
    public int? VideoBitrateKbps { get; init; }

    /// <summary>VBV 최대 비트레이트 상한(kbps). ConstrainedCrf/제약 시. bufsize는 2배 자동.</summary>
    public int? VideoMaxrateKbps { get; init; }

    public VideoSpeedPreset Preset { get; init; } = VideoSpeedPreset.Medium;

    /// <summary>x264/x265: film/animation/grain/zerolatency 등. av1: 0|1|2.</summary>
    public string? Tune { get; init; }

    /// <summary>x264: baseline/main/high/high10 …, x265: main/main10 …. null=자동.</summary>
    public string? Profile { get; init; }

    /// <summary>예: 4.0 / 5.1. null=자동.</summary>
    public string? Level { get; init; }

    /// <summary>yuv420p(8bit·최대호환), yuv420p10le(10bit), yuv444p …</summary>
    public string PixelFormat { get; init; } = "yuv420p";

    /// <summary>키프레임 간격(GOP, 프레임 수). null=인코더 자동.</summary>
    public int? GopSize { get; init; }

    /// <summary>B-프레임 수(CPU 코덱). null=자동.</summary>
    public int? BFrames { get; init; }

    public bool Lossless { get; init; }

    /// <summary>출력 가로 해상도(px). null=원본 유지. 높이는 종횡비 자동(-2).</summary>
    public int? ScaleWidth { get; init; }

    /// <summary>입력이 더 클 때만 축소(업스케일 방지).</summary>
    public bool DownscaleOnly { get; init; } = true;

    public double? Fps { get; init; }

    /// <summary>crop=w:h:x:y (x/y 생략 시 중앙). null=없음.</summary>
    public string? Crop { get; init; }

    public RotateMode Rotate { get; init; } = RotateMode.None;
    public DeinterlaceMode Deinterlace { get; init; } = DeinterlaceMode.Off;

    /// <summary>hqdn3d | nlmeans. null=없음.</summary>
    public string? Denoise { get; init; }

    /// <summary>mp4/mov 웹 스트리밍 최적화(moov atom 앞으로).</summary>
    public bool FastStart { get; init; } = true;

    /// <summary>GPU(NVENC 등) 적응형 양자화로 화질 향상.</summary>
    public bool SpatialAq { get; init; } = true;

    public TimeSpan? TrimStart { get; init; }
    public TimeSpan? TrimEnd { get; init; }

    /// <summary>미노출 옵션을 위한 원시 ffmpeg 출력 인자(예: "-x265-params no-sao=1").</summary>
    public string? RawArgs { get; init; }
}

/// <summary>오디오 인코딩 상세 옵션. 영상의 오디오 트랙 + 오디오 전용 출력에 적용.</summary>
public sealed record AudioEncodeOptions
{
    public AudioCodec Codec { get; init; } = AudioCodec.Auto;
    public AudioRateMode RateMode { get; init; } = AudioRateMode.Bitrate;

    public int AudioBitrateKbps { get; init; } = 192;

    /// <summary>MP3(LAME) VBR 품질 0~9 (낮을수록 고품질).</summary>
    public int Mp3VbrQuality { get; init; } = 2;

    /// <summary>OGG Vorbis VBR 품질 -1~10 (높을수록 고품질).</summary>
    public double VorbisVbrQuality { get; init; } = 5.0;

    /// <summary>FLAC 압축 레벨 0~12 (무손실, 크기/속도만 변화).</summary>
    public int FlacCompressionLevel { get; init; } = 5;

    /// <summary>WAV PCM 포맷: pcm_s16le(16bit) / pcm_s24le(24bit) / pcm_f32le(32bit float).</summary>
    public string PcmFormat { get; init; } = "pcm_s16le";

    public int? SampleRate { get; init; }

    /// <summary>채널 수: 1(모노)/2(스테레오)/6(5.1)/8(7.1). null=원본.</summary>
    public int? Channels { get; init; }

    /// <summary>EBU R128 음량 정규화(loudnorm).</summary>
    public bool Loudnorm { get; init; }

    /// <summary>단순 음량 배수(Loudnorm과 상호배타).</summary>
    public double? VolumeGain { get; init; }

    /// <summary>오디오 원본 무손실 패스스루(영상만 재인코딩 — 속도↑).</summary>
    public bool Copy { get; init; }
}
