using FluentValidation;
using Listmate.Application.Abstractions;
using Listmate.Application.Lists;
using Listmate.Domain.Items;
using Microsoft.Extensions.Logging;

namespace Listmate.Application.Sync;

/// <summary>
/// Applies a batch of offline mutations exactly once. The whole batch is one transaction: the processed-mutation log,
/// the items and the bumped list versions commit together, so a replayed batch finds every id in the log and is a no-op.
/// Two batches racing on one list collide on the list's row version; the loser re-reads and tries again.
/// </summary>
public sealed class SyncService(
    IListStore store,
    IListNotifier notifier,
    IValidator<SyncRequest> validator,
    TimeProvider clock,
    ILogger<SyncService> logger)
{
    private const int MaxAttempts = 4;

    public async Task<SyncResponse> ApplyAsync(string userId, SyncRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var (response, changes) = await ApplyOnceAsync(userId, request.Mutations, cancellationToken);
                foreach (var (listId, (version, items)) in changes)
                {
                    await notifier.ItemsChangedAsync(listId, version, items, cancellationToken);
                }

                return response;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                logger.LogInformation("Sync batch hit a concurrent write (attempt {Attempt}); retrying from fresh reads", attempt);
                store.DiscardChanges();
            }
        }
    }

    private async Task<(SyncResponse Response, Dictionary<Guid, (long Version, List<ItemDto> Items)> Changes)> ApplyOnceAsync(
        string userId, IReadOnlyList<MutationDto> mutations, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var seen = new HashSet<Guid>(await store.FindProcessedAsync([.. mutations.Select(m => m.MutationId).Distinct()], cancellationToken));
        var lists = await store.FindListsAsync([.. mutations.Select(m => m.ListId).Distinct()], cancellationToken);
        var items = new Dictionary<Guid, ListItem>(await store.FindItemsAsync([.. mutations.Select(m => m.ItemId).Distinct()], cancellationToken));

        var results = new List<MutationResultDto>(mutations.Count);
        var changed = new Dictionary<Guid, ListItem>();
        foreach (var mutation in mutations)
        {
            var existing = items.GetValueOrDefault(mutation.ItemId);
            if (!seen.Add(mutation.MutationId))
            {
                results.Add(Result(mutation, MutationStatus.Duplicate, "Already applied.", existing));
                continue;
            }

            var outcome = lists.TryGetValue(mutation.ListId, out var list) && list.IsMember(userId)
                ? ConflictRules.Apply(list, ToDomain(mutation), existing, userId, now)
                : new MutationOutcome(MutationStatus.Rejected, null, false, "List not found.");

            if (existing is null && outcome.Item is not null)
            {
                store.Add(outcome.Item);
                items[outcome.Item.Id] = outcome.Item;
            }

            if (outcome.Changed && outcome.Item is not null)
            {
                changed[outcome.Item.Id] = outcome.Item;
            }

            store.Add(new ProcessedMutation(mutation.MutationId, userId, mutation.ListId, outcome.Status.ToString(), now));
            results.Add(Result(mutation, outcome.Status, outcome.Reason, outcome.Item));
        }

        await store.SaveChangesAsync(cancellationToken);

        var versions = lists.Values.Where(l => l.IsMember(userId)).ToDictionary(l => l.Id, l => l.Version);
        var changes = changed.Values
            .GroupBy(i => i.ListId)
            .ToDictionary(g => g.Key, g => (lists[g.Key].Version, g.OrderBy(i => i.Version).Select(ItemDto.From).ToList()));
        return (new SyncResponse(results, versions), changes);
    }

    private static ItemMutation ToDomain(MutationDto m) => m.Type switch
    {
        MutationTypes.Add => new AddItem(m.MutationId, m.ListId, m.ItemId, m.Text!),
        MutationTypes.Edit => new EditItem(m.MutationId, m.ListId, m.ItemId, m.BaseVersion, m.Name, m.Quantity),
        MutationTypes.Check => new CheckItem(m.MutationId, m.ListId, m.ItemId, m.BaseVersion, m.Checked!.Value),
        MutationTypes.Delete => new DeleteItem(m.MutationId, m.ListId, m.ItemId, m.BaseVersion),
        _ => throw new ArgumentOutOfRangeException(nameof(m), m.Type, "Unknown mutation type."),
    };

    private static MutationResultDto Result(MutationDto mutation, MutationStatus status, string? reason, ListItem? item) =>
        new(mutation.MutationId, status.ToString().ToLowerInvariant(), reason, item is null ? null : ItemDto.From(item));
}
