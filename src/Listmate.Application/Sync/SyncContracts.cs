using Listmate.Application.Lists;

namespace Listmate.Application.Sync;

/// <summary>Mutation kinds on the wire.</summary>
public static class MutationTypes
{
    public const string Add = "add";
    public const string Edit = "edit";
    public const string Check = "check";
    public const string Delete = "delete";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Add, Edit, Check, Delete };
}

/// <summary>One queued client change. Only the fields its <see cref="Type"/> needs are read.</summary>
public sealed record MutationDto(
    Guid MutationId,
    string Type,
    Guid ListId,
    Guid ItemId,
    long? BaseVersion = null,
    string? Text = null,
    string? Name = null,
    int? Quantity = null,
    bool? Checked = null);

/// <summary>A batch from a device's outbox, in the order the person made the changes.</summary>
public sealed record SyncRequest(IReadOnlyList<MutationDto> Mutations);

public sealed record MutationResultDto(Guid MutationId, string Status, string? Reason, ItemDto? Item);

/// <param name="Versions">The current version of every list the batch touched, for the client's resync bookmark.</param>
public sealed record SyncResponse(IReadOnlyList<MutationResultDto> Results, IReadOnlyDictionary<Guid, long> Versions);

/// <summary>The idempotency log: a mutation id in this table has been applied and must never be applied again.</summary>
public sealed class ProcessedMutation(Guid mutationId, string userId, Guid listId, string status, DateTimeOffset processedAt)
{
    public Guid MutationId { get; private set; } = mutationId;

    public string UserId { get; private set; } = userId;

    public Guid ListId { get; private set; } = listId;

    public string Status { get; private set; } = status;

    public DateTimeOffset ProcessedAt { get; private set; } = processedAt;
}
