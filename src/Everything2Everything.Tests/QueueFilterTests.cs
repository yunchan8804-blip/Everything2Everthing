using Everything2Everything.Core.Filters;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 실시간 큐 및 히스토리 검색 & 카테고리 필터 TDD 단위 테스트 스위트
/// 파일명/확장자 검색 및 멀티미디어/문서/이미지/데이터 카테고리 분류 로직 검증
/// </summary>
public class QueueFilterTests
{
    [Theory]
    [InlineData("photo.jpg", FilterCategory.Image)]
    [InlineData("vector.svg", FilterCategory.Image)]
    [InlineData("report.docx", FilterCategory.Document)]
    [InlineData("hwp_doc.hwp", FilterCategory.Document)]
    [InlineData("document.pdf", FilterCategory.Document)]
    [InlineData("movie.mp4", FilterCategory.Media)]
    [InlineData("podcast.mp3", FilterCategory.Media)]
    [InlineData("audio.wav", FilterCategory.Media)]
    [InlineData("dataset.csv", FilterCategory.Data)]
    [InlineData("config.json", FilterCategory.Data)]
    public void GetCategory_ClassifiesCorrectCategory(string filename, FilterCategory expected)
    {
        var category = QueueFilterMatcher.GetCategory(filename);
        Assert.Equal(expected, category);
    }

    [Theory]
    [InlineData("photo.jpg", "", FilterCategory.All, true)]
    [InlineData("photo.jpg", "photo", FilterCategory.All, true)]
    [InlineData("photo.jpg", "PHOTO", FilterCategory.All, true)]
    [InlineData("photo.jpg", ".jpg", FilterCategory.All, true)]
    [InlineData("photo.jpg", "video", FilterCategory.All, false)]
    [InlineData("photo.jpg", "", FilterCategory.Image, true)]
    [InlineData("photo.jpg", "", FilterCategory.Document, false)]
    [InlineData("report.docx", "rep", FilterCategory.Document, true)]
    [InlineData("report.docx", "rep", FilterCategory.Image, false)]
    [InlineData("video.mp4", "vid", FilterCategory.Media, true)]
    public void Matches_FiltersBySearchTextAndCategory(string filename, string query, FilterCategory category, bool expected)
    {
        var matches = QueueFilterMatcher.Matches(filename, query, category);
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FilterCollection_ReturnsMatchingItemsOnly()
    {
        var items = new[]
        {
            new Everything2Everything.App.Views.QueueItem { SourcePath = "C:\\a.png", FileName = "a.png" },
            new Everything2Everything.App.Views.QueueItem { SourcePath = "C:\\b.docx", FileName = "b.docx" },
            new Everything2Everything.App.Views.QueueItem { SourcePath = "C:\\c.mp4", FileName = "c.mp4" },
        };

        var filtered = QueueFilterMatcher.Filter(items, item => item.FileName, "", FilterCategory.Image).ToList();
        Assert.Single(filtered);
        Assert.Equal("a.png", filtered[0].FileName);
    }
}
