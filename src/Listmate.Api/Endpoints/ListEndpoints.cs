using System.Security.Claims;
using Listmate.Api.Hosting;
using Listmate.Application.Lists;
using Listmate.Application.Sync;

namespace Listmate.Api.Endpoints;

internal static class ListEndpoints
{
    public static IEndpointRouteBuilder MapListEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().WithTags("Lists");

        api.MapGet("/lists", (ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
            lists.MyListsAsync(user.UserId(), ct));

        api.MapPost("/lists", async (CreateListRequest request, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
        {
            var created = await lists.CreateAsync(user.UserId(), request, ct);
            return Results.Created($"/api/lists/{created.Id}", created);
        });

        api.MapPost("/lists/join", (JoinListRequest request, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
            lists.JoinAsync(user.UserId(), request, ct));

        api.MapGet("/lists/{listId:guid}", (Guid listId, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
            lists.SnapshotAsync(user.UserId(), listId, ct));

        api.MapGet("/lists/{listId:guid}/changes", (Guid listId, long? since, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
            lists.ChangesSinceAsync(user.UserId(), listId, since ?? 0, ct));

        api.MapGet("/lists/{listId:guid}/members", (Guid listId, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
            lists.MembersAsync(user.UserId(), listId, ct));

        api.MapPost("/lists/{listId:guid}/leave", async (Guid listId, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
        {
            await lists.LeaveAsync(user.UserId(), listId, ct);
            return Results.NoContent();
        });

        api.MapDelete("/lists/{listId:guid}/members/{memberId}", async (Guid listId, string memberId, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
        {
            await lists.RemoveMemberAsync(user.UserId(), listId, memberId, ct);
            return Results.NoContent();
        });

        api.MapPost("/lists/{listId:guid}/merge-duplicates", (Guid listId, ClaimsPrincipal user, ListService lists, CancellationToken ct) =>
            lists.MergeDuplicatesAsync(user.UserId(), listId, ct));

        api.MapPost("/sync", (SyncRequest request, ClaimsPrincipal user, SyncService sync, CancellationToken ct) =>
            sync.ApplyAsync(user.UserId(), request, ct));

        return app;
    }
}
