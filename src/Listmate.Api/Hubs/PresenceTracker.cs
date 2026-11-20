namespace Listmate.Api.Hubs;

/// <summary>
/// Who has a list open, per connection (one person on two phones counts once). Kept in memory, so with several API
/// replicas each one knows only its own connections; a Redis-backed tracker is the scale-out follow-up.
/// </summary>
public sealed class PresenceTracker
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Dictionary<string, Seat>> _byList = [];

    public IReadOnlyList<string> Join(Guid listId, string connectionId, string userId, string person)
    {
        lock (_gate)
        {
            if (!_byList.TryGetValue(listId, out var connections))
            {
                connections = [];
                _byList[listId] = connections;
            }

            connections[connectionId] = new Seat(userId, person);
            return People(connections);
        }
    }

    public IReadOnlyList<string> Leave(Guid listId, string connectionId)
    {
        lock (_gate)
        {
            return LeaveLocked(listId, connectionId);
        }
    }

    /// <summary>This replica's connections of one person that have the list open (to evict them when they leave it).</summary>
    public IReadOnlyList<string> ConnectionsOf(Guid listId, string userId)
    {
        lock (_gate)
        {
            return _byList.TryGetValue(listId, out var connections)
                ? [.. connections.Where(c => c.Value.UserId == userId).Select(c => c.Key)]
                : [];
        }
    }

    /// <summary>Removes the connection everywhere; returns each affected list with who is still there.</summary>
    public IReadOnlyList<(Guid ListId, IReadOnlyList<string> People)> Disconnect(string connectionId)
    {
        lock (_gate)
        {
            var affected = _byList.Where(kv => kv.Value.ContainsKey(connectionId)).Select(kv => kv.Key).ToList();
            return [.. affected.Select(listId => (listId, LeaveLocked(listId, connectionId)))];
        }
    }

    private string[] LeaveLocked(Guid listId, string connectionId)
    {
        if (!_byList.TryGetValue(listId, out var connections))
        {
            return [];
        }

        connections.Remove(connectionId);
        if (connections.Count == 0)
        {
            _byList.Remove(listId);
        }

        return People(connections);
    }

    private static string[] People(Dictionary<string, Seat> connections) =>
        [.. connections.Values.Select(s => s.Person).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    private sealed record Seat(string UserId, string Person);
}
