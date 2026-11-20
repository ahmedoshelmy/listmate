namespace Listmate.Domain.Items;

public enum MutationStatus
{
    /// <summary>Written on top of the version the client saw.</summary>
    Applied,

    /// <summary>Written, but someone changed the item since the client saw it; untouched fields keep their newer values.</summary>
    Merged,

    /// <summary>Nothing to do: same value, already exists, or the item was deleted (delete wins over edit).</summary>
    Ignored,

    /// <summary>The mutation cannot be applied (for example an unknown item).</summary>
    Rejected,

    /// <summary>This mutation id was processed before; the replay changed nothing.</summary>
    Duplicate,
}

/// <param name="Item">The item after the mutation (null when it does not exist).</param>
/// <param name="Changed">True when the item was written and must be broadcast.</param>
public sealed record MutationOutcome(MutationStatus Status, ListItem? Item, bool Changed, string? Reason = null);
