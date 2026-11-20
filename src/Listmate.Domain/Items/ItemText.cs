using System.Globalization;
using System.Text.RegularExpressions;

namespace Listmate.Domain.Items;

/// <summary>
/// What a person types into the add box, split into a quantity and a name: "2x milk", "2 x milk", "milk x2" and
/// "3 eggs" all carry a quantity; anything else is quantity 1.
/// </summary>
public sealed partial record ItemText(int Quantity, string Name)
{
    public const int MaxNameLength = 200;
    public const int MaxQuantity = 999;

    public static ItemText Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var cleaned = Whitespace().Replace(text.Trim(), " ");

        var match = LeadingQuantity().Match(cleaned);
        if (!match.Success)
        {
            match = TrailingQuantity().Match(cleaned);
        }

        var (quantity, name) = match.Success
            ? (int.Parse(match.Groups["qty"].Value, CultureInfo.InvariantCulture), match.Groups["name"].Value.Trim())
            : (1, cleaned);

        if (name.Length == 0)
        {
            (quantity, name) = (1, cleaned);
        }

        return new ItemText(Math.Clamp(quantity, 1, MaxQuantity), Truncate(name));
    }

    /// <summary>The key two items must share to count as duplicates: case and spacing do not matter.</summary>
    public static string Key(string name) => Whitespace().Replace(name.Trim(), " ").ToUpperInvariant();

    public override string ToString() => Quantity > 1 ? $"{Quantity}x {Name}" : Name;

    private static string Truncate(string name) => name.Length <= MaxNameLength ? name : name[..MaxNameLength];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(?<qty>\d{1,3})\s*(?:x|×)?\s+(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingQuantity();

    [GeneratedRegex(@"^(?<name>.+?)\s+(?:x|×)\s*(?<qty>\d{1,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingQuantity();
}
