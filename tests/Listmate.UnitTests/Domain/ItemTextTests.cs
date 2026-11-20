using Listmate.Domain.Items;

namespace Listmate.UnitTests.Domain;

public sealed class ItemTextTests
{
    [Theory]
    [InlineData("milk", 1, "milk")]
    [InlineData("2x milk", 2, "milk")]
    [InlineData("2 x milk", 2, "milk")]
    [InlineData("2X Milk", 2, "Milk")]
    [InlineData("milk x2", 2, "milk")]
    [InlineData("eggs x 12", 12, "eggs")]
    [InlineData("3 eggs", 3, "eggs")]
    [InlineData("  oat   milk  ", 1, "oat milk")]
    [InlineData("7up", 1, "7up")]
    [InlineData("2x", 1, "2x")]
    public void Parse_splits_quantity_and_name(string text, int quantity, string name)
    {
        var parsed = ItemText.Parse(text);

        parsed.Quantity.ShouldBe(quantity);
        parsed.Name.ShouldBe(name);
    }

    [Fact]
    public void Parse_never_returns_a_zero_quantity()
    {
        ItemText.Parse("0 apples").Quantity.ShouldBe(1);
    }

    [Fact]
    public void Key_ignores_case_and_spacing()
    {
        ItemText.Key(" Oat  Milk").ShouldBe(ItemText.Key("oat milk"));
    }

    [Fact]
    public void ToString_shows_quantity_only_above_one()
    {
        new ItemText(3, "milk").ToString().ShouldBe("3x milk");
        new ItemText(1, "milk").ToString().ShouldBe("milk");
    }
}
