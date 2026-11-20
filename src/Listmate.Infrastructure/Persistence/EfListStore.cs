using Listmate.Application.Abstractions;
using Listmate.Application.Sync;
using Listmate.Domain.Items;
using Listmate.Domain.Lists;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Listmate.Infrastructure.Persistence;

public sealed class EfListStore(ListmateDbContext db) : IListStore
{
    public Task<SharedList?> FindListAsync(Guid listId, CancellationToken cancellationToken) =>
        db.Lists.Include(l => l.Members).FirstOrDefaultAsync(l => l.Id == listId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, SharedList>> FindListsAsync(IReadOnlyCollection<Guid> listIds, CancellationToken cancellationToken) =>
        await db.Lists.Include(l => l.Members).Where(l => listIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

    public Task<SharedList?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        db.Lists.Include(l => l.Members).FirstOrDefaultAsync(l => l.InviteCode == inviteCode, cancellationToken);

    public async Task<IReadOnlyList<SharedList>> ListsForMemberAsync(string userId, CancellationToken cancellationToken) =>
        await db.Lists.AsNoTracking()
            .Include(l => l.Members)
            .Where(l => l.Members.Any(m => m.UserId == userId))
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ListItem>> FindItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken) =>
        await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);

    public async Task<IReadOnlyList<ListItem>> ItemsAsync(Guid listId, long sinceVersion, bool includeDeleted, CancellationToken cancellationToken) =>
        await db.Items
            .Where(i => i.ListId == listId && i.Version > sinceVersion && (includeDeleted || !i.Deleted))
            .OrderBy(i => i.Version)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> FindProcessedAsync(IReadOnlyCollection<Guid> mutationIds, CancellationToken cancellationToken) =>
        await db.ProcessedMutations.Where(p => mutationIds.Contains(p.MutationId)).Select(p => p.MutationId).ToHashSetAsync(cancellationToken);

    public void Add(SharedList list) => db.Lists.Add(list);

    public void Add(ListItem item) => db.Items.Add(item);

    public void Add(ProcessedMutation processed) => db.ProcessedMutations.Add(processed);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The list changed while this request was writing.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two devices replaying the same mutation id at once: the loser retries and sees it as a duplicate.
            throw new ConcurrencyConflictException("A concurrent request wrote the same key.", ex);
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}
