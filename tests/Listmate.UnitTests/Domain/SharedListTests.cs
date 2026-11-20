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

    private static SharedList ThreeMembers()
    {
        var list = SharedList.Create("Weekly groceries", TestData.Alex, "ABCDEFGH", TestData.Now);
        list.Join(TestData.Sam, TestData.Now.AddMinutes(1));
        list.Join("user-kim", TestData.Now.AddMinutes(2));
        return list;
    }

    [Fact]
    public void An_editor_leaves_and_the_owner_stays()
    {
        var list = ThreeMembers();

        list.Leave(TestData.Sam).ShouldBe(LeaveOutcome.Left);
        list.IsMember(TestData.Sam).ShouldBeFalse();
        list.IsOwner(TestData.Alex).ShouldBeTrue();
        list.Leave(TestData.Sam).ShouldBe(LeaveOutcome.NotAMember);
    }

    [Fact]
    public void An_owner_who_leaves_hands_the_list_to_the_longest_standing_member()
    {
        var list = ThreeMembers();

        list.Leave(TestData.Alex).ShouldBe(LeaveOutcome.LeftAndHandedOver);
        list.IsOwner(TestData.Sam).ShouldBeTrue();
        list.IsOwner("user-kim").ShouldBeFalse();
    }

    [Fact]
    public void The_last_member_out_empties_the_list()
    {
        var list = SharedList.Create("Solo", TestData.Alex, "ABCDEFGH", TestData.Now);

        list.Leave(TestData.Alex).ShouldBe(LeaveOutcome.LastMemberLeft);
        list.Members.ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_owner_removes_people_and_never_themselves()
    {
        var list = ThreeMembers();

        list.RemoveMember(TestData.Sam, "user-kim").ShouldBe(RemoveMemberOutcome.OnlyTheOwnerCanRemove);
        list.RemoveMember(TestData.Alex, TestData.Alex).ShouldBe(RemoveMemberOutcome.UseLeave);
        list.RemoveMember(TestData.Alex, "user-nobody").ShouldBe(RemoveMemberOutcome.NotAMember);
        list.RemoveMember(TestData.Alex, "user-kim").ShouldBe(RemoveMemberOutcome.Removed);
        list.IsMember("user-kim").ShouldBeFalse();
        list.Members.Count.ShouldBe(2);
    }
}
