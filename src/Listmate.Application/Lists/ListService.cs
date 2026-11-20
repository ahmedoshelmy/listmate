using Listmate.Application.Abstractions;
using Listmate.Domain.Items;
using Listmate.Domain.Lists;

namespace Listmate.Application.Lists;

/// <summary>Use cases around lists: create, join by invite code, read snapshots and changes, merge duplicates.</summary>
public sealed class ListService(IListStore store, IListNotifier notifier, TimeProvider clock)
{
    public async Task<ListSummaryDto> CreateAsync(string userId, CreateListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var list = SharedList.Create(request.Name, userId, InviteCodes.New(), clock.GetUtcNow());
        store.Add(list);
        await store.SaveChangesAsync(cancellationToken);
        return ListSummaryDto.From(list);
    }

    public async Task<ListSummaryDto> JoinAsync(string userId, JoinListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var list = await store.FindByInviteCodeAsync(InviteCodes.Normalize(request.InviteCode), cancellationToken)
            ?? throw new ListNotFoundException("No list has this invite code.");
        if (list.Join(userId, clock.GetUtcNow()))
        {
            await store.SaveChangesAsync(cancellationToken);
            await notifier.MemberJoinedAsync(list.Id, userId, cancellationToken);
        }

        return ListSummaryDto.From(list);
    }

    public async Task<IReadOnlyList<ListSummaryDto>> MyListsAsync(string userId, CancellationToken cancellationToken)
    {
        var lists = await store.ListsForMemberAsync(userId, cancellationToken);
        return [.. lists.Select(ListSummaryDto.From)];
    }

    public async Task<bool> IsMemberAsync(string userId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await store.FindListAsync(listId, cancellationToken);
        return list?.IsMember(userId) == true;
    }

    public async Task<ListSnapshotDto> SnapshotAsync(string userId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await RequireMembershipAsync(userId, listId, cancellationToken);
        var items = await store.ItemsAsync(listId, sinceVersion: 0, includeDeleted: false, cancellationToken);
        return new ListSnapshotDto(ListSummaryDto.From(list), list.Version, [.. items.Select(ItemDto.From)]);
    }

    public async Task<ListChangesDto> ChangesSinceAsync(string userId, Guid listId, long since, CancellationToken cancellationToken)
    {
        var list = await RequireMembershipAsync(userId, listId, cancellationToken);
        var items = await store.ItemsAsync(listId, Math.Max(0, since), includeDeleted: true, cancellationToken);
        return new ListChangesDto(listId, since, list.Version, [.. items.Select(ItemDto.From)]);
    }

    /// <summary>Folds duplicate open items ("2x milk" + "milk" = "3x milk") and broadcasts the result.</summary>
    public async Task<ListChangesDto> MergeDuplicatesAsync(string userId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await RequireMembershipAsync(userId, listId, cancellationToken);
        var since = list.Version;
        var items = await store.ItemsAsync(listId, sinceVersion: 0, includeDeleted: false, cancellationToken);
        var changed = DuplicateMerger.Merge(list, items, userId, clock.GetUtcNow());
        var dtos = changed.Select(ItemDto.From).ToList();
        if (dtos.Count > 0)
        {
            await store.SaveChangesAsync(cancellationToken);
            await notifier.ItemsChangedAsync(listId, list.Version, dtos, cancellationToken);
        }

        return new ListChangesDto(listId, since, list.Version, dtos);
    }

    private async Task<SharedList> RequireMembershipAsync(string userId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await store.FindListAsync(listId, cancellationToken);
        return list is not null && list.IsMember(userId) ? list : throw new ListNotFoundException();
    }
}
