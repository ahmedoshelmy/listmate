using Listmate.Application.Abstractions;
using Listmate.Application.Lists;

namespace Listmate.UnitTests.Fakes;

internal sealed class RecordingNotifier : IListNotifier
{
    public List<(Guid ListId, long Version, ItemDto Item)> Changes { get; } = [];

    public List<Guid> Joins { get; } = [];

    public Task ItemsChangedAsync(Guid listId, long listVersion, IReadOnlyList<ItemDto> items, CancellationToken cancellationToken)
    {
        Changes.AddRange(items.Select(i => (listId, listVersion, i)));
        return Task.CompletedTask;
    }

    public Task MemberJoinedAsync(Guid listId, string userId, CancellationToken cancellationToken)
    {
        Joins.Add(listId);
        return Task.CompletedTask;
    }
}
