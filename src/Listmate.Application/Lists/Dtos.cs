using Listmate.Domain.Items;
using Listmate.Domain.Lists;

namespace Listmate.Application.Lists;

public sealed record ItemDto(Guid Id, Guid ListId, string Name, int Quantity, bool Checked, bool Deleted, long Version, string ChangedBy)
{
    public static ItemDto From(ListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemDto(item.Id, item.ListId, item.Name, item.Quantity, item.Checked, item.Deleted, item.Version, item.ChangedBy);
    }
}

public sealed record ListSummaryDto(Guid Id, string Name, string InviteCode, long Version, int MemberCount)
{
    public static ListSummaryDto From(SharedList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new ListSummaryDto(list.Id, list.Name, list.InviteCode, list.Version, list.Members.Count);
    }
}

/// <summary>The full current state of a list: what a device loads the first time it opens it.</summary>
public sealed record ListSnapshotDto(ListSummaryDto List, long Version, IReadOnlyList<ItemDto> Items);

/// <summary>Everything that changed after a version, tombstones included: what a device fetches after a reconnect.</summary>
public sealed record ListChangesDto(Guid ListId, long Since, long Version, IReadOnlyList<ItemDto> Items);

public sealed record MemberDto(string UserId, string Name, string Role, bool IsYou);

public sealed record CreateListRequest(string Name);

public sealed record JoinListRequest(string InviteCode);
