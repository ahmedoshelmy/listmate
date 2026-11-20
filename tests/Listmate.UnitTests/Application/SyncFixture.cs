using FluentValidation;
using Listmate.Application.Lists;
using Listmate.Application.Sync;
using Listmate.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Listmate.UnitTests.Application;

internal sealed class SyncFixture
{
    public const string Alex = "user-alex";
    public const string Sam = "user-sam";
    public const string Stranger = "user-stranger";

    public InMemoryListStore Store { get; } = new();

    public RecordingNotifier Notifier { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 14, 9, 0, 0, TimeSpan.Zero));

    public ListService Lists => new(Store, Notifier, Clock);

    public SyncService Sync => new(Store, Notifier, new SyncRequestValidator(), Clock, NullLogger<SyncService>.Instance);

    public async Task<ListSummaryDto> SharedListAsync()
    {
        var list = await Lists.CreateAsync(Alex, new CreateListRequest("Weekly groceries"), CancellationToken.None);
        await Lists.JoinAsync(Sam, new JoinListRequest(list.InviteCode), CancellationToken.None);
        return list;
    }

    public Task<SyncResponse> SendAsync(string userId, params MutationDto[] mutations) =>
        Sync.ApplyAsync(userId, new SyncRequest(mutations), CancellationToken.None);

    public static MutationDto Add(Guid listId, Guid itemId, string text) =>
        new(Guid.NewGuid(), MutationTypes.Add, listId, itemId, Text: text);

    public static MutationDto Check(Guid listId, Guid itemId, long? baseVersion, bool value = true) =>
        new(Guid.NewGuid(), MutationTypes.Check, listId, itemId, baseVersion, Checked: value);

    public static MutationDto Rename(Guid listId, Guid itemId, long? baseVersion, string name) =>
        new(Guid.NewGuid(), MutationTypes.Edit, listId, itemId, baseVersion, Name: name);

    public static MutationDto Delete(Guid listId, Guid itemId, long? baseVersion) =>
        new(Guid.NewGuid(), MutationTypes.Delete, listId, itemId, baseVersion);

    public static IValidator<SyncRequest> Validator => new SyncRequestValidator();
}
