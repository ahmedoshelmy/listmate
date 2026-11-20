using Listmate.Application.Abstractions;
using Listmate.Domain.Items;
using Listmate.Domain.Lists;

namespace Listmate.Application.Lists;

/// <summary>Use cases around lists: create, join by invite code, members, read snapshots and changes, merge duplicates.</summary>
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

    public async Task<IReadOnlyList<MemberDto>> MembersAsync(string userId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await RequireMembershipAsync(userId, listId, cancellationToken);
        var names = await store.UserNamesAsync([.. list.Members.Select(m => m.UserId)], cancellationToken);
        return
        [
            .. list.Members
                .OrderBy(m => m.Role).ThenBy(m => m.JoinedAt)
                .Select(m => new MemberDto(m.UserId, names.GetValueOrDefault(m.UserId, "someone"), m.Role.ToString(), m.UserId == userId)),
        ];
    }

    /// <summary>Leaves the list; an owner hands it over, and the last one out deletes it.</summary>
    public async Task LeaveAsync(string userId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await RequireMembershipAsync(userId, listId, cancellationToken);
        if (list.Leave(userId) == LeaveOutcome.LastMemberLeft)
        {
            store.Remove(list);
        }

        await store.SaveChangesAsync(cancellationToken);
        await notifier.MemberLeftAsync(listId, userId, cancellationToken);
    }

    /// <summary>The owner removes someone; they lose access at once and their open screens are told.</summary>
    public async Task RemoveMemberAsync(string userId, Guid listId, string memberId, CancellationToken cancellationToken)
    {
        var list = await RequireMembershipAsync(userId, listId, cancellationToken);
        switch (list.RemoveMember(userId, memberId))
        {
            case RemoveMemberOutcome.OnlyTheOwnerCanRemove:
                throw new ListPermissionException();
            case RemoveMemberOutcome.UseLeave:
                throw new ArgumentException("To take yourself off a list, leave it.", nameof(memberId));
            case RemoveMemberOutcome.NotAMember:
                throw new ListNotFoundException("That person is not on this list.");
        }

        await store.SaveChangesAsync(cancellationToken);
        await notifier.MemberLeftAsync(listId, memberId, cancellationToken);
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
