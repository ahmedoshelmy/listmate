using Listmate.Domain.Lists;

namespace Listmate.Domain.Items;

/// <summary>
/// The sync conflict rules in one place (see docs/sync-protocol.md):
/// per-field last-writer-wins in server arrival order, delete wins over edit, adds are idempotent by item id.
/// </summary>
public static class ConflictRules
{
    public static MutationOutcome Apply(SharedList list, ItemMutation mutation, ListItem? existing, string userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(mutation);
        if (mutation.ListId != list.Id || (existing is not null && existing.ListId != list.Id))
        {
            return new MutationOutcome(MutationStatus.Rejected, null, false, "The item belongs to another list.");
        }

        return mutation switch
        {
            AddItem add => Add(list, add, existing, userId, now),
            DeleteItem => Delete(list, existing, userId, now),
            EditItem edit => Change(list, edit, existing, new ItemChange(Clean(edit.Name), edit.Quantity), userId, now),
            CheckItem check => Change(list, check, existing, new ItemChange(Checked: check.Checked), userId, now),
            _ => new MutationOutcome(MutationStatus.Rejected, existing, false, "Unknown mutation."),
        };
    }

    private static MutationOutcome Add(SharedList list, AddItem add, ListItem? existing, string userId, DateTimeOffset now)
    {
        if (existing is not null)
        {
            return new MutationOutcome(MutationStatus.Ignored, existing, false, "The item already exists.");
        }

        var item = ListItem.Create(add.ItemId, list.Id, ItemText.Parse(add.Text), list.NextVersion(), userId, now);
        return new MutationOutcome(MutationStatus.Applied, item, true);
    }

    private static MutationOutcome Delete(SharedList list, ListItem? existing, string userId, DateTimeOffset now)
    {
        if (existing is null)
        {
            return new MutationOutcome(MutationStatus.Rejected, null, false, "Unknown item.");
        }

        if (existing.Deleted)
        {
            return new MutationOutcome(MutationStatus.Ignored, existing, false, "Already deleted.");
        }

        existing.MarkDeleted();
        existing.Stamp(list.NextVersion(), userId, now);
        return new MutationOutcome(MutationStatus.Applied, existing, true);
    }

    private static MutationOutcome Change(
        SharedList list, ItemMutation mutation, ListItem? existing, ItemChange change, string userId, DateTimeOffset now)
    {
        if (existing is null)
        {
            return new MutationOutcome(MutationStatus.Rejected, null, false, "Unknown item.");
        }

        if (existing.Deleted)
        {
            return new MutationOutcome(MutationStatus.Ignored, existing, false, "The item was deleted; delete wins over edit.");
        }

        var stale = mutation.BaseVersion is { } seen && seen < existing.Version;
        if (!existing.Apply(change))
        {
            return new MutationOutcome(MutationStatus.Ignored, existing, false, "No change.");
        }

        existing.Stamp(list.NextVersion(), userId, now);
        return new MutationOutcome(stale ? MutationStatus.Merged : MutationStatus.Applied, existing, true);
    }

    private static string? Clean(string? name) => name is null ? null : ItemText.Parse(name).Name;
}
