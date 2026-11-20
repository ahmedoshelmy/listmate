namespace Listmate.Domain.Lists;

/// <summary>
/// A list several people edit together. The list owns a monotonically increasing <see cref="Version"/>: every change to
/// one of its items takes the next value, so "everything after version N" is a cheap, gap-free resync query.
/// </summary>
public sealed class SharedList
{
    public const int MaxNameLength = 80;

    private readonly List<Membership> _members = [];

    private SharedList(Guid id, string name, string inviteCode, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        InviteCode = inviteCode;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Short code a member shares so someone else can join.</summary>
    public string InviteCode { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Membership> Members => _members;

    public static SharedList Create(string name, string ownerId, string inviteCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inviteCode);
        var list = new SharedList(Guid.NewGuid(), NormalizeName(name), inviteCode, now);
        list._members.Add(new Membership(list.Id, ownerId, MemberRole.Owner, now));
        return list;
    }

    public bool IsMember(string userId) => _members.Exists(m => m.UserId == userId);

    /// <summary>Adds the user as an editor. Joining twice is a no-op, so a retried invite link is harmless.</summary>
    public bool Join(string userId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (IsMember(userId))
        {
            return false;
        }

        _members.Add(new Membership(Id, userId, MemberRole.Editor, now));
        return true;
    }

    public bool IsOwner(string userId) => _members.Exists(m => m.UserId == userId && m.Role == MemberRole.Owner);

    /// <summary>
    /// Takes the user off the list. An owner who leaves hands the list to the longest-standing member, so a list with
    /// members always has an owner; the last one out leaves an empty list for the caller to delete.
    /// </summary>
    public LeaveOutcome Leave(string userId)
    {
        var member = _members.Find(m => m.UserId == userId);
        if (member is null)
        {
            return LeaveOutcome.NotAMember;
        }

        _members.Remove(member);
        if (_members.Count == 0)
        {
            return LeaveOutcome.LastMemberLeft;
        }

        if (member.Role != MemberRole.Owner || _members.Exists(m => m.Role == MemberRole.Owner))
        {
            return LeaveOutcome.Left;
        }

        _members.OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId, StringComparer.Ordinal).First().PromoteToOwner();
        return LeaveOutcome.LeftAndHandedOver;
    }

    /// <summary>The owner takes someone else off the list; their devices lose access on their next request.</summary>
    public RemoveMemberOutcome RemoveMember(string ownerId, string memberId)
    {
        if (!IsOwner(ownerId))
        {
            return RemoveMemberOutcome.OnlyTheOwnerCanRemove;
        }

        if (ownerId == memberId)
        {
            return RemoveMemberOutcome.UseLeave;
        }

        return _members.RemoveAll(m => m.UserId == memberId) == 1 ? RemoveMemberOutcome.Removed : RemoveMemberOutcome.NotAMember;
    }

    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Claims the next list version for a change that is about to be written.</summary>
    public long NextVersion() => ++Version;

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"A list name has at most {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }
}
