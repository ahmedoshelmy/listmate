using Listmate.Domain.Items;
using Listmate.Domain.Lists;

namespace Listmate.UnitTests.Domain;

internal static class TestData
{
    public const string Alex = "user-alex";
    public const string Sam = "user-sam";
    public static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    public static SharedList List()
    {
        var list = SharedList.Create("Weekly groceries", Alex, "ABCDEFGH", Now);
        list.Join(Sam, Now);
        return list;
    }

    public static ListItem Item(SharedList list, string text, DateTimeOffset? createdAt = null) =>
        ListItem.Create(Guid.NewGuid(), list.Id, ItemText.Parse(text), list.NextVersion(), Alex, createdAt ?? Now);
}
