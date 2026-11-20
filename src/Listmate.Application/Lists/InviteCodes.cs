using System.Security.Cryptography;

namespace Listmate.Application.Lists;

/// <summary>Eight letters from an alphabet without look-alikes (no I, O): easy to read aloud or type from a screenshot.</summary>
public static class InviteCodes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int Length = 8;

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static string Normalize(string code) => (code ?? string.Empty).Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
