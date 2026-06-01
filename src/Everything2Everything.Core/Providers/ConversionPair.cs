namespace Everything2Everything.Core.Providers;

/// <summary>
/// 변환 1홉의 품질 손실 등급. 멀티홉 경로 선택(Dijkstra 가중치)과 UI '손실 변환' 배지의 단일 출처(SSOT).
/// 손실은 곱셈적이므로 가중치를 -log(보존율) 근사로 두면 덧셈 최단경로가 곧 최대 품질보존 경로가 된다.
/// </summary>
public enum LossClass
{
    /// <summary>무손실 — 픽셀/내용 완전 보존 (예: png→png 메타 정리, 컨테이너 무손실 재포장).</summary>
    Lossless,
    /// <summary>컨테이너/구조 변경, 내용 보존 (예: pdf 구조 재압축, tiff 압축 방식 변경).</summary>
    Container,
    /// <summary>재인코딩/손실 압축 (예: jpg 품질 인코딩, docx→pdf 렌더).</summary>
    Recode,
    /// <summary>래스터화 — 벡터/텍스트의 편집성 상실 (예: pdf/svg→png). 단방향 손실 절벽.</summary>
    Rasterize,
}

/// <summary>LossClass → 그래프 엣지 가중치. 클수록 회피된다.</summary>
public static class LossWeights
{
    /// <summary>홉마다 가산되는 페널티. 동일 품질이면 홉이 적은 경로를 선호하게 한다.</summary>
    public const double HopPenalty = 0.1;

    public static double Of(LossClass loss) => loss switch
    {
        LossClass.Lossless => 0.05,
        LossClass.Container => 0.15,
        LossClass.Recode => 0.50,
        LossClass.Rasterize => 1.20,
        _ => 0.50,
    };
}

/// <summary>단일 변환 쌍 (입력 확장자 → 출력 확장자) + 품질 손실 등급.</summary>
public sealed record ConversionPair(string InputExtension, string OutputExtension, LossClass Loss = LossClass.Recode)
{
    public static ConversionPair Of(string input, string output, LossClass loss = LossClass.Recode)
        => new(Normalize(input), Normalize(output), loss);

    public static string Normalize(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            throw new ArgumentException("확장자가 비어 있습니다.", nameof(ext));
        var trimmed = ext.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
