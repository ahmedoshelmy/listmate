using Listmate.Domain.Items;

namespace Listmate.UnitTests.Domain;

public sealed class DuplicateMergerTests
{
    [Fact]
    public void Two_x_milk_plus_milk_is_three_x_milk()
    {
        var list = TestData.List();
        var first = TestData.Item(list, "2x milk", TestData.Now);
        var second = TestData.Item(list, "milk", TestData.Now.AddMinutes(5));

        var changed = DuplicateMerger.Merge(list, [first, second], TestData.Sam, TestData.Now);

        first.Quantity.ShouldBe(3);
        first.Deleted.ShouldBeFalse();
        second.Deleted.ShouldBeTrue();
        changed.ShouldBe([first, second]);
    }

    [Fact]
    public void The_oldest_item_survives_whatever_the_order()
    {
        var list = TestData.List();
        var newer = TestData.Item(list, "Milk", TestData.Now.AddHours(1));
        var older = TestData.Item(list, "milk", TestData.Now);

        DuplicateMerger.Merge(list, [newer, older], TestData.Alex, TestData.Now);

        older.Deleted.ShouldBeFalse();
        older.Quantity.ShouldBe(2);
        newer.Deleted.ShouldBeTrue();
    }

    [Fact]
    public void Ticked_and_distinct_items_are_left_alone()
    {
        var list = TestData.List();
        var ticked = TestData.Item(list, "bread");
        ticked.Apply(new ItemChange(Checked: true));
        var open = TestData.Item(list, "bread");
        var other = TestData.Item(list, "eggs");

        var changed = DuplicateMerger.Merge(list, [ticked, open, other], TestData.Alex, TestData.Now);

        changed.ShouldBeEmpty();
        ticked.Deleted.ShouldBeFalse();
        open.Deleted.ShouldBeFalse();
    }

    [Fact]
    public void Every_change_gets_a_new_list_version()
    {
        var list = TestData.List();
        var a = TestData.Item(list, "milk");
        var b = TestData.Item(list, "milk");
        var before = list.Version;

        DuplicateMerger.Merge(list, [a, b], TestData.Alex, TestData.Now);

        list.Version.ShouldBe(before + 2);
        new[] { a.Version, b.Version }.ShouldBe([before + 1, before + 2], ignoreOrder: true);
    }
}
