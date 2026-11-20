namespace Listmate.Domain.Items;

/// <summary>
/// One line on a list. The id is chosen by the client, so an item created offline keeps its identity when it syncs.
/// Deleting leaves a tombstone (<see cref="Deleted"/>) so other devices learn about the delete on their next resync.
/// </summary>
public sealed class ListItem
{
    private ListItem(Guid id, Guid listId, string name, int quantity, string changedBy, DateTimeOffset createdAt)
    {
        Id = id;
        ListId = listId;
        Name = name;
        Quantity = quantity;
        ChangedBy = changedBy;
        CreatedAt = createdAt;
        ChangedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ListId { get; private set; }

    public string Name { get; private set; }

    public int Quantity { get; private set; }

    public bool Checked { get; private set; }

    public bool Deleted { get; private set; }

    /// <summary>The list version at this item's latest change.</summary>
    public long Version { get; private set; }

    public string ChangedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public static ListItem Create(Guid id, Guid listId, ItemText text, long version, string userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ListItem(id, listId, text.Name, text.Quantity, userId, now) { Version = version };
    }

    /// <summary>Sets only the fields the change carries; returns false when nothing would change.</summary>
    public bool Apply(ItemChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var name = change.Name ?? Name;
        var quantity = change.Quantity ?? Quantity;
        var isChecked = change.Checked ?? Checked;
        if (name == Name && quantity == Quantity && isChecked == Checked)
        {
            return false;
        }

        (Name, Quantity, Checked) = (name, quantity, isChecked);
        return true;
    }

    public void MarkDeleted() => Deleted = true;

    public void Stamp(long version, string userId, DateTimeOffset now)
    {
        Version = version;
        ChangedBy = userId;
        ChangedAt = now;
    }
}

/// <summary>A partial update: null means "not touched by this change".</summary>
public sealed record ItemChange(string? Name = null, int? Quantity = null, bool? Checked = null);
