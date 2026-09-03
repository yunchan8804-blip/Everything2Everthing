using System.IO;
using Everything2Everything.Core.Filters;
using Everything2Everything.Core.Inspector;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 파일 인스펙터(Inspector & Metadata Card) TDD 단위 테스트 스위트
/// 선택된 파일의 세부 메타데이터, 카테고리, 변환 가능 대상 형식 카운트, 탐색기 연동 정보 검증
/// </summary>
public class FileInspectorTests
{
    [Fact]
    public void Build_ValidTextFile_ExtractsMetadataAndTargetFormats()
    {
        var tempFile = Path.GetTempFileName() + ".txt";
        File.WriteAllText(tempFile, "Hello World Everything2Everything Test");

        try
        {
            var info = FileInspectorBuilder.Build(tempFile);

            Assert.Equal(Path.GetFileName(tempFile), info.FileName);
            Assert.Equal(tempFile, info.FullPath);
            Assert.Equal(FilterCategory.Document, info.Category);
            Assert.True(info.FileSizeBytes > 0);
            Assert.NotEmpty(info.FormattedSize);
            Assert.True(info.CanOpen);
            Assert.True(info.CanReveal);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Build_NonExistentFile_ReturnsSafeDefaults()
    {
        var ghostFile = "C:\\path\\does_not_exist\\nonexistent.png";
        var info = FileInspectorBuilder.Build(ghostFile);

        Assert.Equal("nonexistent.png", info.FileName);
        Assert.Equal(FilterCategory.Image, info.Category);
        Assert.Equal(0, info.FileSizeBytes);
        Assert.Equal("0 B", info.FormattedSize);
        Assert.False(info.CanOpen);
        Assert.False(info.CanReveal);
    }

    [Theory]
    [InlineData(".png", FilterCategory.Image)]
    [InlineData(".mp4", FilterCategory.Media)]
    [InlineData(".docx", FilterCategory.Document)]
    [InlineData(".json", FilterCategory.Data)]
    public void Build_IdentifiesCategoryAccurately(string ext, FilterCategory expectedCategory)
    {
        var dummyPath = "C:\\test\\sample" + ext;
        var info = FileInspectorBuilder.Build(dummyPath);
        Assert.Equal(expectedCategory, info.Category);
    }
}
