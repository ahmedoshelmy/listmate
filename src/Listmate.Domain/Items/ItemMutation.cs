namespace Listmate.Domain.Items;

/// <summary>
/// One change a client made, possibly while offline. <see cref="MutationId"/> is the client's idempotency key;
/// <see cref="BaseVersion"/> is the item version the client last saw (null when it never saw the item).
/// </summary>
public abstract record ItemMutation(Guid MutationId, Guid ListId, Guid ItemId, long? BaseVersion);

public sealed record AddItem(Guid MutationId, Guid ListId, Guid ItemId, string Text)
    : ItemMutation(MutationId, ListId, ItemId, null);

public sealed record EditItem(Guid MutationId, Guid ListId, Guid ItemId, long? BaseVersion, string? Name, int? Quantity)
    : ItemMutation(MutationId, ListId, ItemId, BaseVersion);

public sealed record CheckItem(Guid MutationId, Guid ListId, Guid ItemId, long? BaseVersion, bool Checked)
    : ItemMutation(MutationId, ListId, ItemId, BaseVersion);

public sealed record DeleteItem(Guid MutationId, Guid ListId, Guid ItemId, long? BaseVersion)
    : ItemMutation(MutationId, ListId, ItemId, BaseVersion);
