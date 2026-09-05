using System;
using System.Collections.Generic;

namespace Everything2Everything.Core.Presets;

/// <summary>
/// 출력 형식에 맞춰 동적으로 제공되는 세부 프리셋 정보 및 기술 스펙
/// </summary>
public sealed record FormatPreset(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> SpecChips,
    Action<dynamic> ApplyAction)
{
    public void Apply(dynamic options) => ApplyAction(options);
}

/// <summary>
/// 확장자별 최적화 프리셋을 공급하는 스마트 프리셋 엔진 (TDD SSOT)
/// </summary>
public static class FormatPresetEngine
{
    public static IReadOnlyList<FormatPreset> GetPresetsForExtension(string? outputExtension)
    {
        var ext = (outputExtension ?? "").Trim().ToLowerInvariant();
        if (!ext.StartsWith('.') && !string.IsNullOrEmpty(ext)) ext = "." + ext;

        return ext switch
        {
            ".mp3" => Mp3Presets(),
            ".m4a" or ".aac" => AacPresets(ext),
            ".flac" or ".wav" => LosslessAudioPresets(ext),
            ".ogg" or ".opus" => OggOpusPresets(ext),

            ".webp" => WebpPresets(),
            ".avif" => AvifPresets(),
            ".jpg" or ".jpeg" => JpgPresets(),
            ".png" => PngPresets(),
            ".gif" => GifPresets(),
            ".heic" => HeicPresets(),

            ".pdf" => PdfPresets(),

            ".mp4" => Mp4Presets(),
            ".mkv" or ".webm" or ".mov" or ".avi" => GeneralVideoPresets(ext),

            ".csv" or ".xlsx" or ".json" => DataPresets(ext),
            ".docx" or ".html" or ".md" or ".txt" => DocumentPresets(ext),

            _ => FallbackPresets(ext)
        };
    }

    private static IReadOnlyList<FormatPreset> Mp3Presets() => new List<FormatPreset>
    {
        new(
            "mp3-high-320",
            "최고음질 320k (스튜디오 마스터링)",
            "CBR 320kbps · 48kHz · 고해상도 음악 및 마스터링 보존",
            new[] { "320 kbps", "48 kHz", "CBR", "스테레오" },
            opt => { opt.AudioBitrateKbps = 320; opt.AudioVbr = false; opt.SampleRateIndex = 2; opt.ChannelsIndex = 2; }
        ),
        new(
            "mp3-standard-192",
            "표준 스트리밍 192k (일반 감상)",
            "VBR 192kbps · 44.1kHz · 음질과 용량의 최적 균형",
            new[] { "192 kbps", "44.1 kHz", "VBR", "스테레오" },
            opt => { opt.AudioBitrateKbps = 192; opt.AudioVbr = true; opt.SampleRateIndex = 1; opt.ChannelsIndex = 2; }
        ),
        new(
            "mp3-voice-128",
            "팟캐스트·음성 128k (보이스 최적화)",
            "CBR 128kbps · 모노 다운믹스 · 라우드니스 정규화",
            new[] { "128 kbps", "모노", "음성 선명화", "Loudnorm" },
            opt => { opt.AudioBitrateKbps = 128; opt.AudioVbr = false; opt.ChannelsIndex = 1; opt.Loudnorm = true; }
        ),
        new(
            "mp3-compact-64",
            "초절약 압축 64k (용량 극소화)",
            "CBR 64kbps · 32kHz · 녹음본 보관 및 가벼운 전송",
            new[] { "64 kbps", "모노", "초경량 보관" },
            opt => { opt.AudioBitrateKbps = 96; opt.AudioVbr = false; opt.ChannelsIndex = 1; }
        )
    };

    private static IReadOnlyList<FormatPreset> AacPresets(string ext) => new List<FormatPreset>
    {
        new(
            "aac-high-320",
            "최고음질 320k (Apple/고음질)",
            $"{ext.ToUpperInvariant()} 고비트레이트 · 48kHz · 투명한 음질",
            new[] { "320 kbps", "48 kHz", "AAC LC", "스테레오" },
            opt => { opt.AudioBitrateKbps = 320; opt.AudioVbr = false; opt.SampleRateIndex = 2; opt.ChannelsIndex = 2; }
        ),
        new(
            "aac-standard-192",
            "표준 192k (모바일·웹)",
            $"{ext.ToUpperInvariant()} 192kbps · 44.1kHz 표준 인코딩",
            new[] { "192 kbps", "44.1 kHz", "표준" },
            opt => { opt.AudioBitrateKbps = 192; opt.AudioVbr = false; opt.SampleRateIndex = 1; opt.ChannelsIndex = 2; }
        ),
        new(
            "aac-compact-128",
            "가벼운 공유 128k",
            "128kbps 효율적 압축 전송용",
            new[] { "128 kbps", "경량화" },
            opt => { opt.AudioBitrateKbps = 128; opt.ChannelsIndex = 2; }
        )
    };

    private static IReadOnlyList<FormatPreset> LosslessAudioPresets(string ext) => new List<FormatPreset>
    {
        new(
            "lossless-master",
            "무손실 스튜디오 마스터 (24-bit 96kHz)",
            $"{ext.ToUpperInvariant()} 원음 무손실 아카이빙",
            new[] { "무손실 100%", "24-bit", "96 kHz" },
            opt => { opt.AudioBitrateKbps = 320; opt.SampleRateIndex = 0; opt.ChannelsIndex = 0; }
        ),
        new(
            "lossless-cd",
            "CD 음질 무손실 (16-bit 44.1kHz)",
            $"{ext.ToUpperInvariant()} 표준 CD 무손실 추출",
            new[] { "무손실", "16-bit", "44.1 kHz" },
            opt => { opt.AudioBitrateKbps = 320; opt.SampleRateIndex = 1; opt.ChannelsIndex = 2; }
        )
    };

    private static IReadOnlyList<FormatPreset> OggOpusPresets(string ext) => new List<FormatPreset>
    {
        new(
            "opus-high",
            "고음질 보이스/음악 (192k)",
            $"{ext.ToUpperInvariant()} 최신 고효율 코덱",
            new[] { "192 kbps", "차세대 코덱" },
            opt => { opt.AudioBitrateKbps = 192; }
        ),
        new(
            "opus-voice",
            "음성 스트리밍 (96k)",
            "디스코드/보이스 최적화",
            new[] { "96 kbps", "초저지연" },
            opt => { opt.AudioBitrateKbps = 96; }
        )
    };

    private static IReadOnlyList<FormatPreset> WebpPresets() => new List<FormatPreset>
    {
        new(
            "webp-web-q85",
            "웹 고화질 (Q85 · 추천)",
            "품질 85 · EXIF 메타데이터 제거 · 웹 게시 표준",
            new[] { "품질 85", "EXIF 제거", "웹 최적화" },
            opt => { opt.ImageQuality = 85; opt.StripMetadata = true; }
        ),
        new(
            "webp-compact-q65",
            "웹 초경량 (Q65 · 빠른 로딩)",
            "품질 65 · 고압축 이미지로 첫 페이지 로딩 가속",
            new[] { "품질 65", "EXIF 제거", "초경량" },
            opt => { opt.ImageQuality = 65; opt.StripMetadata = true; }
        ),
        new(
            "webp-lossless",
            "무손실 그래픽 (Lossless 100%)",
            "100% 무손실 픽셀 보존 · 알파 투명도 완벽 보존",
            new[] { "무손실 100%", "투명도 보존", "그래픽·아이콘" },
            opt => { opt.ImageQuality = 100; opt.StripMetadata = false; }
        ),
        new(
            "webp-sns-thumb",
            "SNS 썸네일 (Q75)",
            "피드 및 카드 썸네일 최적화",
            new[] { "품질 75", "썸네일" },
            opt => { opt.ImageQuality = 75; opt.StripMetadata = true; }
        ),
        new(
            "webp-extreme-q50",
            "초절약 압축 (Q50)",
            "대역폭 극소화 및 모바일 웹 가속",
            new[] { "품질 50", "대역폭 절약", "초경량" },
            opt => { opt.ImageQuality = 50; opt.StripMetadata = true; }
        )
    };

    private static IReadOnlyList<FormatPreset> AvifPresets() => new List<FormatPreset>
    {
        new(
            "avif-balanced",
            "차세대 초고압축 (Q55 · 추천)",
            "AV1 코덱 기반 압축률 극대화 · 웹 표준",
            new[] { "품질 55", "AV1 코덱", "초고압축" },
            opt => { opt.ImageQuality = 85; opt.StripMetadata = true; }
        ),
        new(
            "avif-high",
            "고화질 아카이빙 (Q75)",
            "색상 심도 10-bit HDR 보존 및 디테일 유지",
            new[] { "품질 75", "10-bit HDR", "고화질" },
            opt => { opt.ImageQuality = 95; opt.StripMetadata = false; }
        ),
        new(
            "avif-web-stream",
            "웹 스트리밍 경량 (Q60)",
            "빠른 디코딩 및 현대적 웹 브라우저 가속",
            new[] { "품질 60", "웹 스트리밍", "빠른 로딩" },
            opt => { opt.ImageQuality = 70; opt.StripMetadata = true; }
        ),
        new(
            "avif-compact-q45",
            "극소 용량 보관 (Q45)",
            "초고효율 AV1 압축으로 최소 용량 달성",
            new[] { "품질 45", "초절약", "극소 용량" },
            opt => { opt.ImageQuality = 55; opt.StripMetadata = true; }
        )
    };

    private static IReadOnlyList<FormatPreset> JpgPresets() => new List<FormatPreset>
    {
        new(
            "jpg-photo-q95",
            "디지털 인화·고화질 (Q95)",
            "품질 95 · 색상 프로파일 보존 · 선명한 사진",
            new[] { "품질 95", "ICC 보존", "고해상도" },
            opt => { opt.ImageQuality = 95; opt.StripMetadata = false; }
        ),
        new(
            "jpg-web-q80",
            "웹 표준 (Q80 · 권장)",
            "품질 80 · 프로그레시브 JPEG · 메타데이터 제거",
            new[] { "품질 80", "EXIF 제거", "프로그레시브" },
            opt => { opt.ImageQuality = 80; opt.StripMetadata = true; }
        ),
        new(
            "jpg-compact-q70",
            "모바일 메신저 (Q70)",
            "품질 70 · 카카오톡/문자 전송 가벼운 용량",
            new[] { "품질 70", "용량 절약", "모바일 최적화" },
            opt => { opt.ImageQuality = 70; opt.StripMetadata = true; }
        ),
        new(
            "jpg-archive-q100",
            "아카이빙 무손실급 (Q100)",
            "최고 화질 보존 · 메타데이터 및 ICC 완전 유지",
            new[] { "품질 100", "원본 보존", "무손실급" },
            opt => { opt.ImageQuality = 100; opt.StripMetadata = false; }
        ),
        new(
            "jpg-thumb-q60",
            "경량 썸네일 (Q60)",
            "빠른 로딩을 위한 인덱스 썸네일 전용",
            new[] { "품질 60", "썸네일", "초경량" },
            opt => { opt.ImageQuality = 60; opt.StripMetadata = true; }
        )
    };

    private static IReadOnlyList<FormatPreset> PngPresets() => new List<FormatPreset>
    {
        new(
            "png-max-compress",
            "최대 압축 무손실 (Level 9)",
            "PNG Deflate 최대 압축 · 무손실 픽셀 완전 보존",
            new[] { "PNG Level 9", "무손실", "디테일 100%" },
            opt => { opt.ImageQuality = 100; opt.StripMetadata = false; }
        ),
        new(
            "png-web-graphic",
            "웹 그래픽 최적화",
            "불필요한 청크 제거 · 알파 투명도 보존",
            new[] { "웹 최적화", "투명도 보존", "EXIF 제거" },
            opt => { opt.ImageQuality = 100; opt.StripMetadata = true; }
        ),
        new(
            "png-clean-alpha",
            "클린 알파 투명도 보존",
            "아이콘, 로고, UI 에셋 투명 레이어 무손실",
            new[] { "알파 채널", "아이콘/UI", "투명도 무손실" },
            opt => { opt.ImageQuality = 100; opt.StripMetadata = false; }
        ),
        new(
            "png-uncompressed",
            "무압축 고속 출력",
            "압축 딜레이 없는 즉시 저장 및 렌더링",
            new[] { "고속 저장", "무손실", "CPU 절약" },
            opt => { opt.ImageQuality = 100; opt.StripMetadata = false; }
        )
    };

    private static IReadOnlyList<FormatPreset> GifPresets() => new List<FormatPreset>
    {
        new(
            "gif-web-256",
            "웹 애니메이션 표준 (256색)",
            "적응형 팔레트 · 디더링 적용으로 선명한 색감",
            new[] { "256 Colors", "디더링", "웹 표준" },
            opt => { opt.Quality = 85; }
        ),
        new(
            "gif-compact-128",
            "초경량 메신저 GIF (128색)",
            "용량 축소 팔레트 · 프레임 레이트 최적화",
            new[] { "128 Colors", "용량 절약", "메신저" },
            opt => { opt.Quality = 65; }
        ),
        new(
            "gif-sharp-64",
            "고압축 그래픽 (64색)",
            "심플 아이콘 및 UI 애니메이션 극소 용량",
            new[] { "64 Colors", "초소형", "UI 그래픽" },
            opt => { opt.Quality = 50; }
        )
    };

    private static IReadOnlyList<FormatPreset> HeicPresets() => new List<FormatPreset>
    {
        new(
            "heic-original-q90",
            "Apple 고화질 보존 (Q90)",
            "아이폰 원본급 HEVC 압축 · 라이브 포토 및 HDR 보존",
            new[] { "Quality 90", "Apple HEVC", "HDR 보존" },
            opt => { opt.ImageQuality = 90; opt.StripMetadata = false; }
        ),
        new(
            "heic-balanced-q80",
            "균형 공유 (Q80 · 추천)",
            "표준 HEIF 압축 · 호환성과 용량 균형",
            new[] { "Quality 80", "표준 압축", "용량 절약" },
            opt => { opt.ImageQuality = 80; opt.StripMetadata = true; }
        ),
        new(
            "heic-compact-q65",
            "초경량 아카이빙 (Q65)",
            "대용량 사진첩 백업용 극소 용량",
            new[] { "Quality 65", "초경량", "백업 전용" },
            opt => { opt.ImageQuality = 65; opt.StripMetadata = true; }
        )
    };

    private static IReadOnlyList<FormatPreset> PdfPresets() => new List<FormatPreset>
    {
        new(
            "pdf-print-300",
            "인쇄용 고해상도 (300 DPI)",
            "300 DPI 래스터/벡터 폰트 임베딩 · 인쇄소 출력용",
            new[] { "300 DPI", "벡터 폰트", "최고품질" },
            opt => { opt.Quality = 100; }
        ),
        new(
            "pdf-web-150",
            "웹 배포·문서 공유 (150 DPI)",
            "150 DPI 텍스트 선명도 및 적정 용량 밸런스",
            new[] { "150 DPI", "문서 공유", "경량화" },
            opt => { opt.Quality = 85; }
        ),
        new(
            "pdf-ebook-96",
            "모바일·전자책 (96 DPI)",
            "96 DPI 초경량화 · 빠른 스크롤 및 이메일 첨부",
            new[] { "96 DPI", "초경량", "이메일 첨부" },
            opt => { opt.Quality = 70; }
        ),
        new(
            "pdf-bw-scan",
            "흑백 스캔 압축 (문서 전용)",
            "그레이스케일 고압축 · 텍스트 가독성 강화",
            new[] { "그레이스케일", "고압축" },
            opt => { opt.Quality = 60; }
        )
    };

    private static IReadOnlyList<FormatPreset> Mp4Presets() => new List<FormatPreset>
    {
        new(
            "mp4-web-1080p",
            "웹 스트리밍 1080p (추천)",
            "H.264 Fast · CRF 23 · AAC 192k · 웹/브라우저 재생",
            new[] { "1080p", "H.264 Fast", "CRF 23", "AAC 192k" },
            opt => { opt.VideoCrf = 23; opt.VideoPreset = "fast"; opt.ResolutionIndex = 3; opt.AudioBitrateKbps = 192; }
        ),
        new(
            "mp4-sns-720p",
            "SNS 메신저 720p (10MB 타겟)",
            "H.264 VeryFast · CRF 26 · AAC 128k · 모바일 가벼운 공유",
            new[] { "720p", "CRF 26", "AAC 128k", "용량 절약" },
            opt => { opt.VideoCrf = 26; opt.VideoPreset = "veryfast"; opt.ResolutionIndex = 4; opt.AudioBitrateKbps = 128; }
        ),
        new(
            "mp4-master-crf18",
            "초고화질 마스터 (CRF 18)",
            "원본 해상도 유지 · H.264 Medium · 오디오 무손실 복사",
            new[] { "원본 해상도", "CRF 18", "Medium", "고화질 보존" },
            opt => { opt.VideoCrf = 18; opt.VideoPreset = "medium"; opt.ResolutionIndex = 0; opt.AudioBitrateKbps = 320; }
        ),
        new(
            "mp4-4k-uhd",
            "4K UHD 아카이빙 (CRF 16)",
            "H.264 High Profile · 마스터링 고화질 아카이빙",
            new[] { "4K UHD", "CRF 16", "마스터링", "고비트레이트" },
            opt => { opt.VideoCrf = 16; opt.VideoPreset = "slow"; opt.ResolutionIndex = 1; opt.AudioBitrateKbps = 320; }
        ),
        new(
            "mp4-audio-extract",
            "오디오 트랙 추출 (AAC)",
            "영상에서 오디오만 고음질로 분리",
            new[] { "오디오 전용", "AAC 256k" },
            opt => { opt.AudioBitrateKbps = 256; }
        )
    };

    private static IReadOnlyList<FormatPreset> GeneralVideoPresets(string ext) => new List<FormatPreset>
    {
        new(
            "video-standard",
            $"{ext.ToUpperInvariant()} 표준 인코딩",
            "CRF 23 · Fast 프리셋",
            new[] { "CRF 23", "Fast", ext },
            opt => { opt.VideoCrf = 23; opt.VideoPreset = "fast"; }
        ),
        new(
            "video-quality",
            $"{ext.ToUpperInvariant()} 고화질 보존",
            "CRF 18 · Medium 프리셋",
            new[] { "CRF 18", "Medium" },
            opt => { opt.VideoCrf = 18; opt.VideoPreset = "medium"; }
        )
    };

    private static IReadOnlyList<FormatPreset> DataPresets(string ext) => new List<FormatPreset>
    {
        new(
            "data-standard",
            "표준 UTF-8 인코딩",
            $"{ext.ToUpperInvariant()} 표준 서식 정리",
            new[] { "UTF-8", "표준 포맷" },
            _ => { }
        ),
        new(
            "data-excel-compat",
            "Excel 호환 서식 (BOM 포함)",
            "Excel에서 한글 깨짐 없는 UTF-8 BOM",
            new[] { "UTF-8 BOM", "엑셀 호환" },
            _ => { }
        )
    };

    private static IReadOnlyList<FormatPreset> DocumentPresets(string ext) => new List<FormatPreset>
    {
        new(
            "doc-standard",
            "표준 문서 서식",
            $"{ext.ToUpperInvariant()} 기본 텍스트/스타일 변환",
            new[] { "표준 서식", ext },
            _ => { }
        ),
        new(
            "doc-clean",
            "서식 단순화 (경량)",
            "군더더기 태그 및 서식 정돈",
            new[] { "클린 서식", "경량화" },
            _ => { }
        )
    };

    private static IReadOnlyList<FormatPreset> FallbackPresets(string ext) => new List<FormatPreset>
    {
        new(
            "default-standard",
            "기본 표준 변환",
            $"{ext} 기본 파라미터 적용",
            new[] { "기본값", ext },
            opt => { opt.Quality = 85; }
        ),
        new(
            "default-quality",
            "품질 우선 변환",
            "최고 품질 설정 적용",
            new[] { "고품질", "무손실 우선" },
            opt => { opt.Quality = 100; }
        )
    };
}
