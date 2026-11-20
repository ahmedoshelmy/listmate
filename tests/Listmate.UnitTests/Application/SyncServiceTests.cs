using FluentValidation;
using Listmate.Application.Abstractions;
using Listmate.Application.Sync;

namespace Listmate.UnitTests.Application;

public sealed class SyncServiceTests
{
    private readonly SyncFixture _f = new();

    [Fact]
    public async Task A_replayed_mutation_id_is_a_no_op()
    {
        var list = await _f.SharedListAsync();
        var itemId = Guid.NewGuid();
        var batch = new[] { SyncFixture.Add(list.Id, itemId, "milk"), SyncFixture.Check(list.Id, itemId, null) };

        var first = await _f.SendAsync(SyncFixture.Alex, batch);
        var notified = _f.Notifier.Changes.Count;
        var replay = await _f.SendAsync(SyncFixture.Alex, batch);

        first.Results.Select(r => r.Status).ShouldBe(["applied", "applied"]);
        replay.Results.Select(r => r.Status).ShouldBe(["duplicate", "duplicate"]);
        replay.Versions[list.Id].ShouldBe(first.Versions[list.Id]);
        replay.Results[1].Item!.Checked.ShouldBeTrue();
        _f.Notifier.Changes.Count.ShouldBe(notified);
        _f.Store.Processed.Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_same_mutation_twice_in_one_batch_applies_once()
    {
        var list = await _f.SharedListAsync();
        var add = SyncFixture.Add(list.Id, Guid.NewGuid(), "milk");

        var response = await _f.SendAsync(SyncFixture.Alex, add, add);

        response.Results.Select(r => r.Status).ShouldBe(["applied", "duplicate"]);
        response.Versions[list.Id].ShouldBe(1);
    }

    [Fact]
    public async Task A_stale_base_version_merges_instead_of_overwriting()
    {
        var list = await _f.SharedListAsync();
        var itemId = Guid.NewGuid();
        var added = await _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, itemId, "milk"));
        var seen = added.Results[0].Item!.Version;

        // Both edited version `seen` while offline; Alex syncs first.
        await _f.SendAsync(SyncFixture.Alex, SyncFixture.Rename(list.Id, itemId, seen, "oat milk"));
        var sam = await _f.SendAsync(SyncFixture.Sam, SyncFixture.Check(list.Id, itemId, seen));

        sam.Results[0].Status.ShouldBe("merged");
        sam.Results[0].Item!.Name.ShouldBe("oat milk");
        sam.Results[0].Item!.Checked.ShouldBeTrue();
        sam.Results[0].Item!.Version.ShouldBe(sam.Versions[list.Id]);
    }

    [Fact]
    public async Task An_offline_edit_of_a_deleted_item_loses_to_the_delete()
    {
        var list = await _f.SharedListAsync();
        var itemId = Guid.NewGuid();
        var seen = (await _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, itemId, "milk"))).Results[0].Item!.Version;
        await _f.SendAsync(SyncFixture.Alex, SyncFixture.Delete(list.Id, itemId, seen));

        var sam = await _f.SendAsync(SyncFixture.Sam, SyncFixture.Rename(list.Id, itemId, seen, "milk 2l"));

        sam.Results[0].Status.ShouldBe("ignored");
        sam.Results[0].Item!.Deleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Non_members_cannot_write_to_a_list()
    {
        var list = await _f.SharedListAsync();

        var response = await _f.SendAsync(SyncFixture.Stranger, SyncFixture.Add(list.Id, Guid.NewGuid(), "milk"));

        response.Results[0].Status.ShouldBe("rejected");
        response.Versions.ShouldNotContainKey(list.Id);
        _f.Store.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changes_are_broadcast_once_per_item_after_commit()
    {
        var list = await _f.SharedListAsync();
        var itemId = Guid.NewGuid();

        await _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, itemId, "milk"), SyncFixture.Check(list.Id, itemId, null));

        var change = _f.Notifier.Changes.ShouldHaveSingleItem();
        change.Item.Checked.ShouldBeTrue();
        change.Version.ShouldBe(2);
    }

    [Fact]
    public async Task A_concurrent_write_is_retried_from_fresh_reads()
    {
        var list = await _f.SharedListAsync();
        _f.Store.ConflictsToSimulate = 2;

        var response = await _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, Guid.NewGuid(), "milk"));

        response.Results[0].Status.ShouldBe("applied");
        _f.Store.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Gives_up_after_repeated_conflicts()
    {
        var list = await _f.SharedListAsync();
        _f.Store.ConflictsToSimulate = 10;

        await Should.ThrowAsync<ConcurrencyConflictException>(() =>
            _f.SendAsync(SyncFixture.Alex, SyncFixture.Add(list.Id, Guid.NewGuid(), "milk")));
    }

    [Fact]
    public async Task Invalid_batches_are_refused_before_anything_is_written()
    {
        var list = await _f.SharedListAsync();
        var bad = new MutationDto(Guid.NewGuid(), "rename", list.Id, Guid.NewGuid());

        await Should.ThrowAsync<ValidationException>(() => _f.SendAsync(SyncFixture.Alex, bad));
        await Should.ThrowAsync<ValidationException>(() => _f.SendAsync(SyncFixture.Alex));
        _f.Store.Processed.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(MutationTypes.Add, null, null, null)]
    [InlineData(MutationTypes.Edit, null, null, null)]
    [InlineData(MutationTypes.Edit, null, 0, null)]
    [InlineData(MutationTypes.Check, null, null, null)]
    public void The_validator_requires_the_fields_of_each_type(string type, string? name, int? quantity, bool? isChecked)
    {
        var mutation = new MutationDto(Guid.NewGuid(), type, Guid.NewGuid(), Guid.NewGuid(), Name: name, Quantity: quantity, Checked: isChecked);

        SyncFixture.Validator.Validate(new SyncRequest([mutation])).IsValid.ShouldBeFalse();
    }
}
