using Listmate.Domain.Lists;

namespace Listmate.UnitTests.Domain;

public sealed class SharedListTests
{
    [Fact]
    public void The_creator_is_the_owner_and_joining_twice_is_a_no_op()
    {
        var list = SharedList.Create("  Beach trip packing ", TestData.Alex, "ABCDEFGH", TestData.Now);

        list.Name.ShouldBe("Beach trip packing");
        list.Members.Single().Role.ShouldBe(MemberRole.Owner);
        list.Join(TestData.Sam, TestData.Now).ShouldBeTrue();
        list.Join(TestData.Sam, TestData.Now).ShouldBeFalse();
        list.Members.Count.ShouldBe(2);
    }

    [Fact]
    public void Versions_only_go_up()
    {
        var list = TestData.List();

        list.NextVersion().ShouldBe(1);
        list.NextVersion().ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_list_needs_a_name(string name)
    {
        Should.Throw<ArgumentException>(() => SharedList.Create(name, TestData.Alex, "ABCDEFGH", TestData.Now));
    }
}
