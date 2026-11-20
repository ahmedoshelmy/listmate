using Listmate.Application.Abstractions;
using Listmate.Application.Sync;
using Listmate.Domain.Items;
using Listmate.Domain.Lists;

namespace Listmate.UnitTests.Fakes;

/// <summary>Keeps committed state in dictionaries; added entities become visible on SaveChanges, like a real unit of work.</summary>
internal sealed class InMemoryListStore : IListStore
{
    private readonly List<object> _pending = [];

    public Dictionary<Guid, SharedList> Lists { get; } = [];

    public Dictionary<Guid, ListItem> Items { get; } = [];

    public Dictionary<Guid, ProcessedMutation> Processed { get; } = [];

    public int SaveCount { get; private set; }

    /// <summary>Makes the next N saves fail as if another request had changed the list first.</summary>
    public int ConflictsToSimulate { get; set; }

    public Task<SharedList?> FindListAsync(Guid listId, CancellationToken cancellationToken) =>
        Task.FromResult(Lists.GetValueOrDefault(listId));

    public Task<IReadOnlyDictionary<Guid, SharedList>> FindListsAsync(IReadOnlyCollection<Guid> listIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, SharedList>>(Lists.Where(kv => listIds.Contains(kv.Key)).ToDictionary());

    public Task<SharedList?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        Task.FromResult(Lists.Values.FirstOrDefault(l => l.InviteCode == inviteCode));

    public Task<IReadOnlyList<SharedList>> ListsForMemberAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SharedList>>([.. Lists.Values.Where(l => l.IsMember(userId))]);

    public Task<IReadOnlyDictionary<Guid, ListItem>> FindItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ListItem>>(Items.Where(kv => itemIds.Contains(kv.Key)).ToDictionary());

    public Task<IReadOnlyList<ListItem>> ItemsAsync(Guid listId, long sinceVersion, bool includeDeleted, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ListItem>>(
            [.. Items.Values.Where(i => i.ListId == listId && i.Version > sinceVersion && (includeDeleted || !i.Deleted)).OrderBy(i => i.Version)]);

    public Task<IReadOnlySet<Guid>> FindProcessedAsync(IReadOnlyCollection<Guid> mutationIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Processed.Keys.Where(mutationIds.Contains).ToHashSet());

    public void Add(SharedList list) => _pending.Add(list);

    public void Add(ListItem item) => _pending.Add(item);

    public void Add(ProcessedMutation processed) => _pending.Add(processed);

    public void Remove(SharedList list) => _pending.Add(new Removal(list));

    public Task<IReadOnlyDictionary<string, string>> UserNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(userIds.ToDictionary(id => id, id => $"{id}@example.com"));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ConflictsToSimulate > 0)
        {
            ConflictsToSimulate--;
            throw new ConcurrencyConflictException();
        }

        foreach (var entity in _pending)
        {
            switch (entity)
            {
                case SharedList list: Lists[list.Id] = list; break;
                case ListItem item: Items[item.Id] = item; break;
                case ProcessedMutation processed: Processed.Add(processed.MutationId, processed); break;
                case Removal removal: Lists.Remove(removal.List.Id); break;
            }
        }

        _pending.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }

    public void DiscardChanges() => _pending.Clear();

    private sealed record Removal(SharedList List);
}
