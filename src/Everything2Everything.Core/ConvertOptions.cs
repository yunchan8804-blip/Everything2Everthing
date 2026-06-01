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

public sealed class ConvertOptions
{
    public OutputLocation OutputLocation { get; set; } = OutputLocation.SubfolderBesideSource;

    public string SubfolderSuffix { get; set; } = "_converted";

    public string? CustomOutputDirectory { get; set; }

    public NameCollision OnCollision { get; set; } = NameCollision.AppendNumber;

    public int? MaxLongEdgePixels { get; set; }

    public bool KeepExifWhenPossible { get; set; } = true;

    public bool FlattenTransparency { get; set; } = false;

    public string TransparencyBackground { get; set; } = "#FFFFFF";

    public JpegEncodingOptions Jpeg { get; set; } = new();

    public PngEncodingOptions Png { get; set; } = new();

    public WebpEncodingOptions Webp { get; set; } = new();

    public AvifEncodingOptions Avif { get; set; } = new();

    public TiffEncodingOptions Tiff { get; set; } = new();

    public BmpEncodingOptions Bmp { get; set; } = new();

    public GifEncodingOptions Gif { get; set; } = new();

    public PdfRenderOptions PdfRender { get; set; } = new();

    public PdfBuildOptions PdfBuild { get; set; } = new();

    public HtmlRenderOptions HtmlRender { get; set; } = new();

    public OcrOptions Ocr { get; set; } = new();

    // --- 변환 그래프 경로 옵션 (P1) ---
    /// <summary>멀티홉 경로 자동 합성 허용. false면 직접(1홉) 변환만.</summary>
    public bool AllowMultiHop { get; set; } = true;

    /// <summary>멀티홉 최대 홉 수.</summary>
    public int MaxHops { get; set; } = 3;

    /// <summary>래스터화 같은 큰 손실 엣지를 회피한다.</summary>
    public bool AvoidLossy { get; set; } = false;

    public PdfCompressOptions PdfCompress { get; set; } = new();

    public AiOptions Ai { get; set; } = new();

    public static ConvertOptions Quick() => new();
}

public sealed class JpegEncodingOptions
{
    public int Quality { get; set; } = 92;
    public bool Progressive { get; set; } = false;
}

public sealed class PngEncodingOptions
{
    public int Compression { get; set; } = 7;
    public bool Interlace { get; set; } = false;
}

public sealed class WebpEncodingOptions
{
    public int Quality { get; set; } = 90;
    public bool Lossless { get; set; } = false;
}

public sealed class AvifEncodingOptions
{
    public int Quality { get; set; } = 60;
    public int Speed { get; set; } = 6;
}

public sealed class TiffEncodingOptions
{
    public string Compression { get; set; } = "lzw";
}

public sealed class BmpEncodingOptions
{
}

public sealed class GifEncodingOptions
{
}

public sealed class PdfRenderOptions
{
    public int Dpi { get; set; } = 200;
    public bool WithAnnotations { get; set; } = true;
    public bool WithFormFill { get; set; } = true;
}

public sealed class PdfBuildOptions
{
    public string PageSize { get; set; } = "Auto";
    public int MarginPoints { get; set; } = 24;
    public bool FitToPage { get; set; } = true;
}

public sealed class HtmlRenderOptions
{
    public int ViewportWidth { get; set; } = 1280;
    public int? ViewportHeight { get; set; }
    public int WaitMilliseconds { get; set; } = 2000;
    public bool FullPage { get; set; } = true;
}

public sealed class OcrOptions
{
    public string Language { get; set; } = "ko+en";
    public bool PreserveLayout { get; set; } = true;
    public string Backend { get; set; } = "auto";
}

public sealed class PdfCompressOptions
{
    /// <summary>Light(구조 최적화·무손실) | Strong(렌더 재인코딩) | Max(Ghostscript). P1은 Light만 구현.</summary>
    public string Level { get; set; } = "Light";
}

public sealed class AiOptions
{
    /// <summary>auto | openai | anthropic. auto는 설정된 키 중 가용한 것을 선택.</summary>
    public string Backend { get; set; } = "auto";

    /// <summary>모델 ID. null이면 백엔드별 기본값.</summary>
    public string? Model { get; set; }

    /// <summary>summarize | translate | proofread | custom.</summary>
    public string Task { get; set; } = "summarize";

    /// <summary>translate 작업의 대상 언어 (예: "영어", "일본어").</summary>
    public string? TargetLanguage { get; set; }

    /// <summary>custom 작업의 사용자 지정 지시문.</summary>
    public string? Instruction { get; set; }

    public int MaxOutputTokens { get; set; } = 2000;
}
