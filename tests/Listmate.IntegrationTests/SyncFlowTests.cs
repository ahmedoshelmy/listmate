using System.Net;
using System.Net.Http.Json;
using Listmate.Api.Hubs;
using Listmate.Application.Lists;
using Listmate.Application.Sync;
using Listmate.Infrastructure.Retention;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Listmate.IntegrationTests;

[Collection(ListmateFixtureGroup.Name)]
public sealed class SyncFlowTests(ListmateApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task User_B_receives_ItemChanged_when_user_A_ticks_an_item()
    {
        var (alex, sam, list) = await SharedListAsync();
        var milk = Guid.NewGuid();
        await SyncAsync(alex, Add(list.Id, milk, "2x milk"));

        await using var hub = app.Hub(sam);
        var ticked = new TaskCompletionSource<ItemChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var presence = new TaskCompletionSource<PresenceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<ItemChangedMessage>(nameof(IListsClient.ItemChanged), m =>
        {
            if (m.Item.Id == milk && m.Item.Checked)
            {
                ticked.TrySetResult(m);
            }
        });
        hub.On<PresenceMessage>(nameof(IListsClient.PresenceChanged), m => presence.TrySetResult(m));
        await hub.StartAsync();
        await hub.InvokeAsync(nameof(ListsHub.JoinList), list.Id);

        await SyncAsync(alex, Check(list.Id, milk));

        var message = await ticked.Task.WaitAsync(Timeout);
        message.ListId.ShouldBe(list.Id);
        message.Item.Quantity.ShouldBe(2);
        message.Item.ChangedBy.ShouldNotBeNullOrEmpty();
        (await presence.Task.WaitAsync(Timeout)).People.ShouldBe([sam.Email]);
    }

    [Fact]
    public async Task An_offline_batch_of_five_replayed_twice_is_applied_once()
    {
        var (alex, _, list) = await SharedListAsync();
        var (milk, eggs, bread) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var batch = new SyncRequest(
        [
            Add(list.Id, milk, "milk"),
            Add(list.Id, eggs, "eggs x12"),
            Add(list.Id, bread, "bread"),
            Check(list.Id, milk),
            new MutationDto(Guid.NewGuid(), MutationTypes.Edit, list.Id, bread, Name: "rye bread"),
        ]);

        // The phone comes back online and its outbox flushes twice at once (reconnect + manual retry), then once more.
        var racing = await Task.WhenAll(PostSyncAsync(alex, batch), PostSyncAsync(alex, batch));
        var replay = await PostSyncAsync(alex, batch);

        racing.SelectMany(r => r.Results).Count(r => r.Status == "applied").ShouldBe(5);
        racing.SelectMany(r => r.Results).Count(r => r.Status == "duplicate").ShouldBe(5);
        replay.Results.ShouldAllBe(r => r.Status == "duplicate");
        (await app.ScalarAsync("select count(*) from list_items where \"ListId\" = @list", list.Id)).ShouldBe(3);
        (await app.ScalarAsync("select count(*) from processed_mutations where \"ListId\" = @list", list.Id)).ShouldBe(5);
        (await app.ScalarAsync("select \"Version\" from lists where \"Id\" = @list", list.Id)).ShouldBe(5);

        var snapshot = await alex.Http.GetFromJsonAsync<ListSnapshotDto>($"/api/lists/{list.Id}");
        snapshot!.Items.Select(i => (i.Name, i.Quantity, i.Checked)).ShouldBe(
            [("milk", 1, true), ("eggs", 12, false), ("rye bread", 1, false)], ignoreOrder: true);
    }

    [Fact]
    public async Task A_device_that_missed_messages_resyncs_with_changes_since_its_version()
    {
        var (alex, sam, list) = await SharedListAsync();
        var milk = Guid.NewGuid();
        var bookmark = (await PostSyncAsync(sam, new SyncRequest([Add(list.Id, milk, "milk")]))).Versions[list.Id];

        // While Sam is offline, Alex deletes milk and adds two more lines.
        await SyncAsync(alex, new MutationDto(Guid.NewGuid(), MutationTypes.Delete, list.Id, milk), Add(list.Id, Guid.NewGuid(), "milk"));
        await SyncAsync(alex, Add(list.Id, Guid.NewGuid(), "2x milk"));

        var changes = await sam.Http.GetFromJsonAsync<ListChangesDto>($"/api/lists/{list.Id}/changes?since={bookmark}");
        changes!.Items.Count.ShouldBe(3);
        changes.Items.ShouldContain(i => i.Id == milk && i.Deleted);
        changes.Version.ShouldBe(bookmark + 3);

        var merged = await (await sam.Http.PostAsync($"/api/lists/{list.Id}/merge-duplicates", null)).Content.ReadFromJsonAsync<ListChangesDto>();
        merged!.Items.ShouldContain(i => !i.Deleted && i.Quantity == 3);
    }

    [Fact]
    public async Task Strangers_and_anonymous_callers_are_kept_out()
    {
        var (_, _, list) = await SharedListAsync();
        var stranger = await app.SignUpAsync("stranger");

        (await app.Factory.CreateClient().GetAsync("/api/lists")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await stranger.Http.GetAsync($"/api/lists/{list.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Http.PostAsJsonAsync("/api/lists/join", new JoinListRequest("ZZZZZZZZ"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var invalid = await stranger.Http.PostAsJsonAsync("/api/sync", new SyncRequest([]));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await using var hub = app.Hub(stranger);
        await hub.StartAsync();
        await Should.ThrowAsync<HubException>(() => hub.InvokeAsync(nameof(ListsHub.JoinList), list.Id));
    }

    [Fact]
    public async Task A_removed_member_is_told_live_then_gets_no_more_changes_and_no_access()
    {
        var (alex, sam, list) = await SharedListAsync();
        var members = await sam.Http.GetFromJsonAsync<MemberDto[]>($"/api/lists/{list.Id}/members");
        members!.Select(m => (m.Name, m.Role)).ShouldBe([(alex.Email, "Owner"), (sam.Email, "Editor")]);
        var samId = members!.Single(m => m.IsYou).UserId;

        await using var hub = app.Hub(sam);
        var removed = new TaskCompletionSource<MemberLeftMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changesAfterRemoval = 0;
        hub.On<MemberLeftMessage>(nameof(IListsClient.MemberLeft), m => removed.TrySetResult(m));
        hub.On<ItemChangedMessage>(nameof(IListsClient.ItemChanged), _ => Interlocked.Increment(ref changesAfterRemoval));
        await hub.StartAsync();
        await hub.InvokeAsync(nameof(ListsHub.JoinList), list.Id);

        var alexId = members!.Single(m => !m.IsYou).UserId;
        (await sam.Http.DeleteAsync($"/api/lists/{list.Id}/members/{alexId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await alex.Http.DeleteAsync($"/api/lists/{list.Id}/members/{samId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await removed.Task.WaitAsync(Timeout)).UserId.ShouldBe(samId);
        await SyncAsync(alex, Add(list.Id, Guid.NewGuid(), "after removal"));
        await Task.Delay(TimeSpan.FromSeconds(1));
        changesAfterRemoval.ShouldBe(0);
        (await sam.Http.GetAsync($"/api/lists/{list.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PostSyncAsync(sam, new SyncRequest([Add(list.Id, Guid.NewGuid(), "sneaky")]))).Results.ShouldAllBe(r => r.Status != "applied");
    }

    [Fact]
    public async Task Leaving_hands_over_ownership_and_the_last_one_out_deletes_the_list()
    {
        var (alex, sam, list) = await SharedListAsync();
        await SyncAsync(alex, Add(list.Id, Guid.NewGuid(), "milk"));

        (await alex.Http.PostAsync($"/api/lists/{list.Id}/leave", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await sam.Http.GetFromJsonAsync<MemberDto[]>($"/api/lists/{list.Id}/members"))!.ShouldHaveSingleItem().Role.ShouldBe("Owner");

        (await sam.Http.PostAsync($"/api/lists/{list.Id}/leave", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await app.ScalarAsync("select count(*) from lists where \"Id\" = @list", list.Id)).ShouldBe(0);
        (await app.ScalarAsync("select count(*) from list_items where \"ListId\" = @list", list.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Retention_prunes_only_processed_mutations_past_the_window()
    {
        var listId = Guid.NewGuid();
        (await app.ScalarAsync(
            """
            with inserted as (
              insert into processed_mutations ("MutationId", "UserId", "ListId", "Status", "ProcessedAt")
              values (gen_random_uuid(), 'u', @list, 'applied', now() - interval '45 days'),
                     (gen_random_uuid(), 'u', @list, 'applied', now() - interval '2 days')
              returning 1)
            select count(*) from inserted
            """,
            listId)).ShouldBe(2);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ProcessedMutationPruner>().PruneAsync(CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);

        (await app.ScalarAsync("select count(*) from processed_mutations where \"ListId\" = @list", listId)).ShouldBe(1);
    }

    [Fact]
    public async Task Demo_accounts_share_the_seeded_lists()
    {
        var sam = await app.SignInAsync("sam@example.com");

        var lists = await sam.Http.GetFromJsonAsync<ListSummaryDto[]>("/api/lists");
        lists!.Select(l => l.Name).ShouldBe(["Weekly groceries", "Beach trip packing"], ignoreOrder: true);
        var groceries = await sam.Http.GetFromJsonAsync<ListSnapshotDto>($"/api/lists/{lists!.Single(l => l.Name == "Weekly groceries").Id}");
        groceries!.Items.ShouldContain(i => i.Name == "eggs" && i.Quantity == 12);
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        var http = app.Factory.CreateClient();

        (await http.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await http.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<(User Alex, User Sam, ListSummaryDto List)> SharedListAsync()
    {
        var alex = await app.SignUpAsync("alex");
        var sam = await app.SignUpAsync("sam");
        var created = await alex.Http.PostAsJsonAsync("/api/lists", new CreateListRequest("Weekly groceries"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var list = (await created.Content.ReadFromJsonAsync<ListSummaryDto>())!;
        var joined = await sam.Http.PostAsJsonAsync("/api/lists/join", new JoinListRequest(list.InviteCode));
        joined.EnsureSuccessStatusCode();
        return (alex, sam, list);
    }

    private static async Task SyncAsync(User user, params MutationDto[] mutations)
    {
        var response = await PostSyncAsync(user, new SyncRequest(mutations));
        response.Results.ShouldAllBe(r => r.Status == "applied");
    }

    private static async Task<SyncResponse> PostSyncAsync(User user, SyncRequest request)
    {
        var response = await user.Http.PostAsJsonAsync("/api/sync", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncResponse>())!;
    }

    private static MutationDto Add(Guid listId, Guid itemId, string text) => new(Guid.NewGuid(), MutationTypes.Add, listId, itemId, Text: text);

    private static MutationDto Check(Guid listId, Guid itemId) => new(Guid.NewGuid(), MutationTypes.Check, listId, itemId, Checked: true);
}
