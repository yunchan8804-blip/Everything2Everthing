using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Filters;

/// <summary>
/// 미디어 카테고리(Image, Document, Media, Data) 간의 변환 호환성 및 출력 필터링을 협상(Negotiation)하는 도메인 서비스.
/// 이미지→Word(DOCX) 등 비정상적인 미디어 전이 노출 및 실행을 방지한다.
/// </summary>
public static class MediaConversionNegotiator
{
    private static readonly HashSet<string> AllowedImageToDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", // 이미지 문서 캡슐화 (PDF)
        ".txt", // OCR 텍스트 추출 (TXT)
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus"
    };

    private static readonly HashSet<string> AllowedVideoToImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif",  // 영상 → GIF 애니메이션
        ".png",  // 영상 → 대표 프레임 스틸컷
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp"
    };

    public static bool IsVideo(string ext) => VideoExtensions.Contains(ConversionPair.Normalize(ext));
    public static bool IsAudio(string ext) => AudioExtensions.Contains(ConversionPair.Normalize(ext));

    /// <summary>
    /// 입력 확장자와 출력 확장자 간의 미디어 변환 가능 여부를 판정한다.
    /// </summary>
    public static bool CanConvert(string inputExt, string outputExt)
    {
        var input = ConversionPair.Normalize(inputExt);
        var output = ConversionPair.Normalize(outputExt);

        if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
            return true;

        var inCat = QueueFilterMatcher.GetCategory(input);
        var outCat = QueueFilterMatcher.GetCategory(output);

        // 1. 미디어(영상/음성) 입력 세분화
        if (inCat == FilterCategory.Media)
        {
            var isVideoIn = IsVideo(input);
            var isAudioIn = IsAudio(input);

            if (outCat == FilterCategory.Media)
            {
                var isVideoOut = IsVideo(output);
                var isAudioOut = IsAudio(output);

                if (isVideoIn)
                {
                    // 비디오는 비디오 간 변환 또는 오디오 추출 허용
                    return isVideoOut || isAudioOut;
                }
                if (isAudioIn)
                {
                    // 오디오는 오디오 간 변환만 허용 (오디오→비디오 변환 불가)
                    return isAudioOut;
                }
                return true;
            }

            if (outCat == FilterCategory.Image)
            {
                // 비디오만 GIF 애니메이션 및 대표 프레임 추출 허용. 오디오는 이미지 불가!
                return isVideoIn && AllowedVideoToImageExtensions.Contains(output);
            }

            // 미디어 → 문서/데이터 변환 불가
            return false;
        }

        return (inCat, outCat) switch
        {
            // 2. 이미지 입력
            (FilterCategory.Image, FilterCategory.Image) => true,
            (FilterCategory.Image, FilterCategory.Document) => AllowedImageToDocumentExtensions.Contains(output),
            (FilterCategory.Image, _) => false,

            // 3. 데이터(CSV/JSON/XLSX) 입력: 표 데이터 상호 변환만 허용
            (FilterCategory.Data, FilterCategory.Data) => true,
            (FilterCategory.Data, _) => false,

            // 4. 문서(PDF/DOCX/HWP/HTML/MD/TXT) 입력
            (FilterCategory.Document, FilterCategory.Document) => true,
            (FilterCategory.Document, FilterCategory.Image) => true, // 문서 페이지 렌더링
            (FilterCategory.Document, _) => false,

            // 기본: 동일 카테고리 허용
            _ => inCat == outCat,
        };
    }

    /// <summary>
    /// 후보 출력 확장자 목록 중 입력 미디어와 호환되는 유효한 출력 확장자만 필터링하여 반환한다.
    /// </summary>
    public static IEnumerable<string> FilterAvailableOutputs(string inputExt, IEnumerable<string> candidateOutputs)
    {
        return candidateOutputs.Where(outExt => CanConvert(inputExt, outExt));
    }
}
