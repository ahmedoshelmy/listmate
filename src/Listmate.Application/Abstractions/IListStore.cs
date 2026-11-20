using Listmate.Application.Sync;
using Listmate.Domain.Items;
using Listmate.Domain.Lists;

namespace Listmate.Application.Abstractions;

/// <summary>
/// Persistence port for lists, items and the processed-mutation log. One instance is one unit of work: everything
/// added or changed is written atomically by <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IListStore
{
    Task<SharedList?> FindListAsync(Guid listId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, SharedList>> FindListsAsync(IReadOnlyCollection<Guid> listIds, CancellationToken cancellationToken);

    Task<SharedList?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<SharedList>> ListsForMemberAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ListItem>> FindItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken);

    /// <summary>Items of the list changed after <paramref name="sinceVersion"/>, tombstones included when asked.</summary>
    Task<IReadOnlyList<ListItem>> ItemsAsync(Guid listId, long sinceVersion, bool includeDeleted, CancellationToken cancellationToken);

    /// <summary>Which of these mutation ids were already applied.</summary>
    Task<IReadOnlySet<Guid>> FindProcessedAsync(IReadOnlyCollection<Guid> mutationIds, CancellationToken cancellationToken);

    void Add(SharedList list);

    void Add(ListItem item);

    void Add(ProcessedMutation processed);

    /// <summary>Deletes the list with its items and memberships.</summary>
    void Remove(SharedList list);

    /// <summary>Display names (emails) of these users, for the member list.</summary>
    Task<IReadOnlyDictionary<string, string>> UserNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    /// <exception cref="ConcurrencyConflictException">Another writer changed the same list first.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every tracked change so the unit of work can be retried from fresh reads.</summary>
    void DiscardChanges();
}
