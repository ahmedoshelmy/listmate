using Listmate.Domain.Lists;

namespace Listmate.Domain.Items;

/// <summary>
/// Folds open items that name the same thing into one: "2x milk" + "milk" becomes "3x milk". The oldest item survives
/// (so its id stays stable on every device), the others become tombstones. Ticked items are left alone: they are done.
/// </summary>
public static class DuplicateMerger
{
    public static IReadOnlyList<ListItem> Merge(SharedList list, IEnumerable<ListItem> items, string userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(list);
        var changed = new List<ListItem>();
        var groups = items
            .Where(i => !i.Deleted && !i.Checked && i.ListId == list.Id)
            .GroupBy(i => ItemText.Key(i.Name))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(i => i.CreatedAt).ThenBy(i => i.Version).ToList();
            var keeper = ordered[0];
            var total = Math.Min(ordered.Sum(i => i.Quantity), ItemText.MaxQuantity);
            keeper.Apply(new ItemChange(Quantity: total));
            keeper.Stamp(list.NextVersion(), userId, now);
            changed.Add(keeper);

            foreach (var duplicate in ordered.Skip(1))
            {
                duplicate.MarkDeleted();
                duplicate.Stamp(list.NextVersion(), userId, now);
                changed.Add(duplicate);
            }
        }

        return changed;
    }
}
