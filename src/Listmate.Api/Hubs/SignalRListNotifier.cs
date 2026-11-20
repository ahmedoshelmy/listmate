using Listmate.Application.Abstractions;
using Listmate.Application.Lists;
using Microsoft.AspNetCore.SignalR;

namespace Listmate.Api.Hubs;

internal sealed class SignalRListNotifier(IHubContext<ListsHub, IListsClient> hub, PresenceTracker presence) : IListNotifier
{
    public async Task ItemsChangedAsync(Guid listId, long listVersion, IReadOnlyList<ItemDto> items, CancellationToken cancellationToken)
    {
        var group = hub.Clients.Group(ListsHub.GroupName(listId));
        foreach (var item in items)
        {
            await group.ItemChanged(new ItemChangedMessage(listId, listVersion, item));
        }
    }

    public Task MemberJoinedAsync(Guid listId, string userId, CancellationToken cancellationToken) =>
        hub.Clients.Group(ListsHub.GroupName(listId)).MemberJoined(new MemberJoinedMessage(listId));

    /// <summary>
    /// Tells the group first (so the person's own screens learn they are off the list), then takes their connections on
    /// this replica out of the group so no further changes reach them.
    /// </summary>
    public async Task MemberLeftAsync(Guid listId, string userId, CancellationToken cancellationToken)
    {
        var group = ListsHub.GroupName(listId);
        await hub.Clients.Group(group).MemberLeft(new MemberLeftMessage(listId, userId));
        foreach (var connectionId in presence.ConnectionsOf(listId, userId))
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
            var people = presence.Leave(listId, connectionId);
            await hub.Clients.Group(group).PresenceChanged(new PresenceMessage(listId, people));
        }
    }
}
