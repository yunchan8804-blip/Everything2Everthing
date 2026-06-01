using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Everything2Everything.App.Views;

/// <summary>
/// 파일 확장자 → 카테고리 글리프(imagegen2 생성 PNG) 매핑.
/// 큐 행·미리보기 placeholder 등에서 형식을 한눈에 보여주기 위해 사용한다.
/// </summary>
public static class CategoryGlyphs
{
    private static readonly Dictionary<string, string> ExtToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        // image
        [".jpg"] = "image", [".jpeg"] = "image", [".jpe"] = "image", [".png"] = "image",
        [".webp"] = "image", [".avif"] = "image", [".bmp"] = "image", [".tif"] = "image",
        [".tiff"] = "image", [".gif"] = "image", [".heic"] = "image", [".heif"] = "image",
        [".cr2"] = "image", [".nef"] = "image", [".arw"] = "image", [".dng"] = "image", [".raw"] = "image",
        // vector
        [".svg"] = "vector", [".eps"] = "vector", [".ai"] = "vector",
        // video
        [".mp4"] = "video", [".webm"] = "video", [".mkv"] = "video", [".mov"] = "video", [".avi"] = "video",
        // audio
        [".mp3"] = "audio", [".aac"] = "audio", [".m4a"] = "audio", [".opus"] = "audio",
        [".ogg"] = "audio", [".flac"] = "audio", [".wav"] = "audio",
        // document
        [".pdf"] = "document", [".docx"] = "document", [".doc"] = "document",
        [".hwp"] = "document", [".hwpx"] = "document", [".html"] = "document", [".htm"] = "document",
        [".md"] = "document", [".txt"] = "document", [".rtf"] = "document",
        // data
        [".csv"] = "data", [".json"] = "data", [".xlsx"] = "data", [".xls"] = "data",
        [".tsv"] = "data", [".xml"] = "data", [".yaml"] = "data", [".yml"] = "data",
        // archive
        [".zip"] = "archive", [".7z"] = "archive", [".rar"] = "archive",
        [".tar"] = "archive", [".gz"] = "archive",
    };

    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>확장자(.jpg 등)에 해당하는 카테고리 글리프 이미지. 매핑이 없으면 document로 폴백.</summary>
    public static ImageSource ForExtension(string? extension)
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? "" : extension.Trim();
        if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
        var category = ExtToCategory.TryGetValue(ext, out var c) ? c : "document";
        return ForCategory(category);
    }

    /// <summary>카테고리명(image/video/audio/document/data/vector/archive/ai)에 해당하는 글리프.</summary>
    public static ImageSource ForCategory(string category)
    {
        if (Cache.TryGetValue(category, out var cached)) return cached;
        var uri = new Uri($"pack://application:,,,/Assets/glyph-{category}.png", UriKind.Absolute);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.UriSource = uri;
        img.EndInit();
        img.Freeze();
        Cache[category] = img;
        return img;
    }
}
