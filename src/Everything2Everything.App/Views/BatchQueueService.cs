using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Everything2Everything.App.Views;

public static class BatchQueueService
{
    public static void SetSelectionAll(IEnumerable<QueueItem> items, bool isSelected)
    {
        foreach (var item in items)
        {
            item.IsSelected = isSelected;
        }
    }

    public static void RemoveSelected(IList<QueueItem> items)
    {
        var toRemove = items.Where(i => i.IsSelected).ToList();
        foreach (var item in toRemove)
        {
            items.Remove(item);
        }
    }

    public static void ClearCompleted(IList<QueueItem> items)
    {
        var toRemove = items.Where(i => i.IsDone || i.StateText == "done").ToList();
        foreach (var item in toRemove)
        {
            items.Remove(item);
        }
    }

    public static void BatchChangeOutput(IEnumerable<QueueItem> items, string newOutputExt, IEnumerable<string> eligibleInputExtensions)
    {
        var eligibleSet = new HashSet<string>(
            eligibleInputExtensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
        );

        foreach (var item in items)
        {
            var ext = Path.GetExtension(item.SourcePath).ToLowerInvariant();
            if (eligibleSet.Contains(ext))
            {
                item.SelectedOutputExtension = newOutputExt;
            }
        }
    }
}
