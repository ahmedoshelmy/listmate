using Listmate.Api.Hubs;

namespace Listmate.UnitTests.Api;

public sealed class PresenceTrackerTests
{
    [Fact]
    public void One_person_on_two_devices_counts_once_until_both_leave()
    {
        var tracker = new PresenceTracker();
        var list = Guid.NewGuid();

        tracker.Join(list, "phone", "sam@example.com");
        tracker.Join(list, "laptop", "sam@example.com");
        tracker.Join(list, "c3", "alex@example.com").ShouldBe(["alex@example.com", "sam@example.com"]);

        tracker.Leave(list, "phone").ShouldBe(["alex@example.com", "sam@example.com"]);
        var after = tracker.Disconnect("laptop").ShouldHaveSingleItem();
        after.ListId.ShouldBe(list);
        after.People.ShouldBe(["alex@example.com"]);
    }

    [Fact]
    public void Disconnect_reports_every_list_the_connection_had_open()
    {
        var tracker = new PresenceTracker();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        tracker.Join(a, "c1", "sam@example.com");
        tracker.Join(b, "c1", "sam@example.com");

        tracker.Disconnect("c1").Select(x => x.ListId).ShouldBe([a, b], ignoreOrder: true);
        tracker.Leave(a, "c1").ShouldBeEmpty();
    }
}
