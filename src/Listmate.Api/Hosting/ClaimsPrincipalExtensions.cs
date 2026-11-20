using System.Security.Claims;

namespace Listmate.Api.Hosting;

internal static class ClaimsPrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("The request is not authenticated.");
}
