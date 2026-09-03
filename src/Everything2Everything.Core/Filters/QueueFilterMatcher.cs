using System;
using System.IO;

namespace Everything2Everything.Core.Filters;

public enum FilterCategory
{
    All,
    Image,
    Document,
    Media,
    Data,
}

public static class QueueFilterMatcher
{
    public static FilterCategory GetCategory(string pathOrExt)
    {
        var ext = Path.GetExtension(pathOrExt).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            ext = pathOrExt.Trim().ToLowerInvariant();
            if (!ext.StartsWith('.')) ext = "." + ext;
        }

        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".avif" or ".gif" or ".bmp" or ".tif" or ".tiff"
                or ".svg" or ".heic" or ".heif" or ".raw" or ".dng" or ".cr2" or ".cr3" or ".nef" or ".arw"
                => FilterCategory.Image,

            ".pdf" or ".docx" or ".doc" or ".hwp" or ".hwpx" or ".txt" or ".md" or ".markdown" or ".html" or ".htm" or ".xlsx" or ".xls"
                => FilterCategory.Document,

            ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi" or ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".opus"
                => FilterCategory.Media,

            ".csv" or ".json" or ".tsv" or ".xml" or ".yaml" or ".yml"
                => FilterCategory.Data,

            _ => FilterCategory.All,
        };
    }

    public static bool Matches(string filename, string query, FilterCategory category)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;

        // 1. 카테고리 필터 검사
        if (category != FilterCategory.All)
        {
            var itemCategory = GetCategory(filename);
            if (itemCategory != category) return false;
        }

        // 2. 검색어 필터 검사
        if (string.IsNullOrWhiteSpace(query)) return true;

        var cleanQuery = query.Trim();
        return filename.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase);
    }

    public static System.Collections.Generic.IEnumerable<T> Filter<T>(
        System.Collections.Generic.IEnumerable<T> source,
        Func<T, string> fileNameSelector,
        string query,
        FilterCategory category)
    {
        return System.Linq.Enumerable.Where(source, item => Matches(fileNameSelector(item), query, category));
    }
}
