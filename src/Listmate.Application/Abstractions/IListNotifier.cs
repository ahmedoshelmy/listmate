using Listmate.Application.Lists;

namespace Listmate.Application.Abstractions;

/// <summary>Real-time fan-out to everyone watching a list. Called only after the change is committed.</summary>
public interface IListNotifier
{
    Task ItemsChangedAsync(Guid listId, long listVersion, IReadOnlyList<ItemDto> items, CancellationToken cancellationToken);

    Task MemberJoinedAsync(Guid listId, string userId, CancellationToken cancellationToken);

    /// <summary>Tells the list that someone left or was removed, and stops pushing its changes to that person.</summary>
    Task MemberLeftAsync(Guid listId, string userId, CancellationToken cancellationToken);
}
