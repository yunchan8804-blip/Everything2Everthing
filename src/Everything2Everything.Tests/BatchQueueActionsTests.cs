using System.Collections.ObjectModel;
using System.IO;
using Everything2Everything.App.Views;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 큐 일괄 처리(Batch Actions) TDD 단위 테스트 스위트
/// 전체 선택/해제, 선택 항목 삭제, 완료 항목 정리, 일괄 출력 형식 변경 검증
/// </summary>
public class BatchQueueActionsTests
{
    private static QueueItem CreateItem(string path, string outExt = ".jpg") =>
        new()
        {
            SourcePath = path,
            FileName = Path.GetFileName(path),
            SelectedOutputExtension = outExt,
            StateText = "queued",
        };

    [Fact]
    public void BatchChangeOutput_UpdatesEligibleItems()
    {
        var items = new ObservableCollection<QueueItem>
        {
            CreateItem("a.png", ".jpg"),
            CreateItem("b.png", ".jpg"),
            CreateItem("c.mp4", ".mp4"),
        };

        // .webp 지원 여부에 따라 이미지(a.png, b.png)만 .webp로 일괄 변경
        BatchQueueService.BatchChangeOutput(items, ".webp", new[] { ".png", ".jpg" });

        Assert.Equal(".webp", items[0].SelectedOutputExtension);
        Assert.Equal(".webp", items[1].SelectedOutputExtension);
        Assert.Equal(".mp4", items[2].SelectedOutputExtension); // mp4는 제외
    }

    [Fact]
    public void RemoveSelected_RemovesOnlySelectedItems()
    {
        var items = new ObservableCollection<QueueItem>
        {
            CreateItem("1.png"),
            CreateItem("2.png"),
            CreateItem("3.png"),
        };
        items[0].IsSelected = true;
        items[2].IsSelected = true;

        BatchQueueService.RemoveSelected(items);

        Assert.Single(items);
        Assert.Equal("2.png", items[0].FileName);
    }

    [Fact]
    public void ClearCompleted_RemovesOnlyDoneItems()
    {
        var i1 = CreateItem("1.png"); i1.SetState("done");
        var i2 = CreateItem("2.png"); i2.SetState("45%");
        var i3 = CreateItem("3.png"); i3.SetState("queued");
        var i4 = CreateItem("4.png"); i4.SetState("done");

        var items = new ObservableCollection<QueueItem> { i1, i2, i3, i4 };

        BatchQueueService.ClearCompleted(items);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.False(item.IsDone));
    }

    [Fact]
    public void SelectAll_TogglesAllItems()
    {
        var items = new ObservableCollection<QueueItem>
        {
            CreateItem("1.png"),
            CreateItem("2.png"),
        };

        BatchQueueService.SetSelectionAll(items, true);
        Assert.All(items, item => Assert.True(item.IsSelected));

        BatchQueueService.SetSelectionAll(items, false);
        Assert.All(items, item => Assert.False(item.IsSelected));
    }
}
