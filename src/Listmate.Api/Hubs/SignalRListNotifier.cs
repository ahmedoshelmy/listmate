using Listmate.Application.Abstractions;
using Listmate.Application.Lists;
using Microsoft.AspNetCore.SignalR;

namespace Listmate.Api.Hubs;

internal sealed class SignalRListNotifier(IHubContext<ListsHub, IListsClient> hub) : IListNotifier
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
}
