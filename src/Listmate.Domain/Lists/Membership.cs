namespace Listmate.Domain.Lists;

public enum MemberRole
{
    Owner,
    Editor,
}

public enum LeaveOutcome
{
    NotAMember,
    Left,

    /// <summary>The owner left; the longest-standing member is the owner now.</summary>
    LeftAndHandedOver,

    /// <summary>Nobody is left: the list should be deleted.</summary>
    LastMemberLeft,
}

public enum RemoveMemberOutcome
{
    Removed,
    NotAMember,
    OnlyTheOwnerCanRemove,

    /// <summary>Removing yourself is leaving.</summary>
    UseLeave,
}

public sealed class Membership(Guid listId, string userId, MemberRole role, DateTimeOffset joinedAt)
{
    public Guid ListId { get; private set; } = listId;

    public string UserId { get; private set; } = userId;

    public MemberRole Role { get; private set; } = role;

    public DateTimeOffset JoinedAt { get; private set; } = joinedAt;

    internal void PromoteToOwner() => Role = MemberRole.Owner;
}
