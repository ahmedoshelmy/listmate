using Listmate.Api.Hosting;
using Listmate.Application.Lists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Listmate.Api.Hubs;

/// <summary>Messages the server pushes to clients watching a list.</summary>
public interface IListsClient
{
    Task ItemChanged(ItemChangedMessage message);

    Task PresenceChanged(PresenceMessage message);

    Task MemberJoined(MemberJoinedMessage message);
}

public sealed record ItemChangedMessage(Guid ListId, long ListVersion, ItemDto Item);

public sealed record PresenceMessage(Guid ListId, IReadOnlyList<string> People);

public sealed record MemberJoinedMessage(Guid ListId);

/// <summary>
/// One SignalR group per list ("list:{id}"). A client calls <see cref="JoinList"/> for the list on screen; after a
/// reconnect it joins again and fetches GET /api/lists/{id}/changes?since=N, so messages missed while away are never lost.
/// </summary>
[Authorize]
public sealed class ListsHub(ListService lists, PresenceTracker presence) : Hub<IListsClient>
{
    public static string GroupName(Guid listId) => $"list:{listId}";

    public async Task JoinList(Guid listId)
    {
        if (!await lists.IsMemberAsync(Context.User!.UserId(), listId, Context.ConnectionAborted))
        {
            throw new HubException("List not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(listId), Context.ConnectionAborted);
        var people = presence.Join(listId, Context.ConnectionId, Context.User?.Identity?.Name ?? "someone");
        await Clients.Group(GroupName(listId)).PresenceChanged(new PresenceMessage(listId, people));
    }

    public async Task LeaveList(Guid listId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(listId), Context.ConnectionAborted);
        var people = presence.Leave(listId, Context.ConnectionId);
        await Clients.Group(GroupName(listId)).PresenceChanged(new PresenceMessage(listId, people));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (listId, people) in presence.Disconnect(Context.ConnectionId))
        {
            await Clients.Group(GroupName(listId)).PresenceChanged(new PresenceMessage(listId, people));
        }

        await base.OnDisconnectedAsync(exception);
    }
}
