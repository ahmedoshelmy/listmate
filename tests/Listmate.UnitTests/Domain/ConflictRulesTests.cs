using Listmate.Domain.Items;

namespace Listmate.UnitTests.Domain;

public sealed class ConflictRulesTests
{
    private readonly Listmate.Domain.Lists.SharedList _list = TestData.List();

    [Fact]
    public void Add_creates_the_item_with_the_client_id_and_the_next_version()
    {
        var itemId = Guid.NewGuid();

        var outcome = ConflictRules.Apply(_list, new AddItem(Guid.NewGuid(), _list.Id, itemId, "2x milk"), null, TestData.Alex, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Applied);
        outcome.Item!.Id.ShouldBe(itemId);
        outcome.Item.Quantity.ShouldBe(2);
        outcome.Item.Version.ShouldBe(_list.Version);
    }

    [Fact]
    public void Adding_an_existing_item_id_is_ignored()
    {
        var item = TestData.Item(_list, "milk");
        var version = _list.Version;

        var outcome = ConflictRules.Apply(_list, new AddItem(Guid.NewGuid(), _list.Id, item.Id, "bread"), item, TestData.Sam, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Ignored);
        item.Name.ShouldBe("milk");
        _list.Version.ShouldBe(version);
    }

    [Fact]
    public void Delete_wins_over_a_later_edit()
    {
        var item = TestData.Item(_list, "milk");
        var seen = item.Version;
        ConflictRules.Apply(_list, new DeleteItem(Guid.NewGuid(), _list.Id, item.Id, seen), item, TestData.Alex, TestData.Now);

        var outcome = ConflictRules.Apply(_list, new CheckItem(Guid.NewGuid(), _list.Id, item.Id, seen, true), item, TestData.Sam, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Ignored);
        outcome.Changed.ShouldBeFalse();
        item.Deleted.ShouldBeTrue();
        item.Checked.ShouldBeFalse();
    }

    [Fact]
    public void Delete_after_an_edit_still_deletes()
    {
        var item = TestData.Item(_list, "milk");
        var seen = item.Version;
        ConflictRules.Apply(_list, new EditItem(Guid.NewGuid(), _list.Id, item.Id, seen, "oat milk", null), item, TestData.Alex, TestData.Now);

        var outcome = ConflictRules.Apply(_list, new DeleteItem(Guid.NewGuid(), _list.Id, item.Id, seen), item, TestData.Sam, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Applied);
        item.Deleted.ShouldBeTrue();
    }

    [Fact]
    public void Stale_edits_merge_per_field_and_the_later_writer_wins_a_shared_field()
    {
        var item = TestData.Item(_list, "milk");
        var seen = item.Version;

        var alex = ConflictRules.Apply(_list, new EditItem(Guid.NewGuid(), _list.Id, item.Id, seen, "oat milk", 2), item, TestData.Alex, TestData.Now);
        var sam = ConflictRules.Apply(_list, new CheckItem(Guid.NewGuid(), _list.Id, item.Id, seen, true), item, TestData.Sam, TestData.Now);
        var late = ConflictRules.Apply(_list, new EditItem(Guid.NewGuid(), _list.Id, item.Id, seen, null, 5), item, TestData.Sam, TestData.Now);

        alex.Status.ShouldBe(MutationStatus.Applied);
        sam.Status.ShouldBe(MutationStatus.Merged);
        late.Status.ShouldBe(MutationStatus.Merged);
        item.Name.ShouldBe("oat milk");
        item.Checked.ShouldBeTrue();
        item.Quantity.ShouldBe(5);
        item.ChangedBy.ShouldBe(TestData.Sam);
    }

    [Fact]
    public void Setting_the_current_value_is_ignored_without_a_new_version()
    {
        var item = TestData.Item(_list, "milk");
        var version = _list.Version;

        var outcome = ConflictRules.Apply(_list, new CheckItem(Guid.NewGuid(), _list.Id, item.Id, item.Version, false), item, TestData.Alex, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Ignored);
        _list.Version.ShouldBe(version);
    }

    [Fact]
    public void Changes_to_unknown_items_are_rejected()
    {
        var outcome = ConflictRules.Apply(_list, new CheckItem(Guid.NewGuid(), _list.Id, Guid.NewGuid(), null, true), null, TestData.Alex, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Rejected);
    }

    [Fact]
    public void An_item_from_another_list_is_rejected()
    {
        var other = TestData.List();
        var foreign = TestData.Item(other, "milk");

        var outcome = ConflictRules.Apply(_list, new DeleteItem(Guid.NewGuid(), _list.Id, foreign.Id, null), foreign, TestData.Alex, TestData.Now);

        outcome.Status.ShouldBe(MutationStatus.Rejected);
        foreign.Deleted.ShouldBeFalse();
    }
}
