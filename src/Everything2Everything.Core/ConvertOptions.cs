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

    /// <summary>영상 인코딩 시 GPU 하드웨어 가속(NVENC)을 우선 시도하고, 실패하면 CPU로 자동 폴백한다.</summary>
    public bool VideoPreferGpu { get; init; } = true;

    public PdfCompressOptions PdfCompress { get; init; } = new();

    public AiOptions Ai { get; init; } = new();

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
