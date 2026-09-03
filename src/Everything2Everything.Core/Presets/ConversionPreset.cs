using System;

namespace Everything2Everything.Core.Presets;

public enum PresetType
{
    WebOptimized,
    HighQualityLossless,
    DocumentPdf,
    MobileShare,
}

public sealed record PresetInfo(PresetType Type, string Title, string Description, string IconSymbol);

public static class ConversionPreset
{
    public static PresetInfo GetInfo(PresetType type) => type switch
    {
        PresetType.WebOptimized => new PresetInfo(type, "웹 최적화", "WebP · 80% 압축 · 메타데이터 제거", "Globe24"),
        PresetType.HighQualityLossless => new PresetInfo(type, "초고화질 보존", "무손실 PNG/FLAC · 100% 품질", "Sparkle24"),
        PresetType.DocumentPdf => new PresetInfo(type, "문서 PDF 보관", "표준 PDF/A 문서 변환", "DocumentPdf24"),
        PresetType.MobileShare => new PresetInfo(type, "모바일 공유", "MP4 H.264 · 가벼운 용량 전송", "Phone24"),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string Apply(PresetType type, dynamic options, string inputExtension)
    {
        var ext = inputExtension.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.')) ext = "." + ext;

        switch (type)
        {
            case PresetType.WebOptimized:
                options.ImageQuality = 80;
                options.StripMetadata = true;
                if (IsVideo(ext)) return ".mp4";
                if (IsAudio(ext)) return ".mp3";
                if (IsDoc(ext)) return ".pdf";
                return ".webp";

            case PresetType.HighQualityLossless:
                options.ImageQuality = 100;
                options.StripMetadata = false;
                options.AudioBitrateKbps = 320;
                if (IsAudio(ext)) return ".flac";
                if (IsVideo(ext)) return ".mkv";
                return ".png";

            case PresetType.DocumentPdf:
                return ".pdf";

            case PresetType.MobileShare:
                options.VideoCrf = 26;
                options.AudioBitrateKbps = 128;
                options.VideoPreset = "veryfast";
                if (IsVideo(ext)) return ".mp4";
                if (IsAudio(ext)) return ".mp3";
                return ".jpg";

            default:
                return ext;
        }
    }

    private static bool IsVideo(string ext) => ext is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi" or ".m4v";
    private static bool IsAudio(string ext) => ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".opus";
    private static bool IsDoc(string ext) => ext is ".docx" or ".doc" or ".hwp" or ".hwpx" or ".txt" or ".md" or ".markdown" or ".html";
}
