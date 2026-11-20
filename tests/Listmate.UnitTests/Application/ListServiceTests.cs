using Listmate.Application.Abstractions;
using Listmate.Application.Lists;

namespace Listmate.UnitTests.Application;

public sealed class ListServiceTests
{
    private readonly SyncFixture _f = new();

    [Fact]
    public async Task Joining_by_invite_code_is_case_and_dash_insensitive_and_idempotent()
    {
        var list = await _f.Lists.CreateAsync(SyncFixture.Alex, new CreateListRequest("Beach trip packing"), CancellationToken.None);
        var code = $"{list.InviteCode[..4].ToLowerInvariant()}-{list.InviteCode[4..]}";

        var joined = await _f.Lists.JoinAsync(SyncFixture.Sam, new JoinListRequest(code), CancellationToken.None);
        await _f.Lists.JoinAsync(SyncFixture.Sam, new JoinListRequest(code), CancellationToken.None);

        joined.MemberCount.ShouldBe(2);
        _f.Notifier.Joins.ShouldBe([list.Id]);
        (await _f.Lists.MyListsAsync(SyncFixture.Sam, CancellationToken.None)).ShouldHaveSingleItem().Id.ShouldBe(list.Id);
    }

    [Fact]
    public void Invite_codes_are_letters_only()
    {
        var code = InviteCodes.New();

        code.Length.ShouldBe(InviteCodes.Length);
        code.ShouldAllBe(c => char.IsAsciiLetterUpper(c) && c != 'I' && c != 'O');
    }

    [Fact]
    public async Task Unknown_codes_and_strangers_get_not_found()
    {
        var list = await _f.SharedListAsync();

        await Should.ThrowAsync<ListNotFoundException>(() => _f.Lists.JoinAsync(SyncFixture.Sam, new JoinListRequest("ZZZZZZZZ"), CancellationToken.None));
        await Should.ThrowAsync<ListNotFoundException>(() => _f.Lists.SnapshotAsync(SyncFixture.Stranger, list.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Changes_since_include_tombstones_but_the_snapshot_does_not()
    {
        var list = await _f.SharedListAsync();
        var milk = Guid.NewGuid();
        await _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, milk, "milk"), SyncFixture.Add(list.Id, Guid.NewGuid(), "bread"));
        await _f.SendAsync(SyncFixture.Sam, SyncFixture.Delete(list.Id, milk, 1));

        var snapshot = await _f.Lists.SnapshotAsync(SyncFixture.Sam, list.Id, CancellationToken.None);
        var changes = await _f.Lists.ChangesSinceAsync(SyncFixture.Alex, list.Id, since: 2, CancellationToken.None);

        snapshot.Version.ShouldBe(3);
        snapshot.Items.Select(i => i.Name).ShouldBe(["bread"]);
        changes.Items.ShouldHaveSingleItem().Deleted.ShouldBeTrue();
        changes.Version.ShouldBe(3);
    }

    [Fact]
    public async Task Merge_duplicates_folds_quantities_and_broadcasts()
    {
        var list = await _f.SharedListAsync();
        await _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, Guid.NewGuid(), "2x milk"));
        _f.Clock.Advance(TimeSpan.FromMinutes(1));
        await _f.SendAsync(SyncFixture.Sam, SyncFixture.Add(list.Id, Guid.NewGuid(), "Milk"));
        _f.Notifier.Changes.Clear();

        var merged = await _f.Lists.MergeDuplicatesAsync(SyncFixture.Sam, list.Id, CancellationToken.None);
        var snapshot = await _f.Lists.SnapshotAsync(SyncFixture.Alex, list.Id, CancellationToken.None);

        merged.Items.Count.ShouldBe(2);
        snapshot.Items.ShouldHaveSingleItem().Quantity.ShouldBe(3);
        _f.Notifier.Changes.Count.ShouldBe(2);
    }
}
