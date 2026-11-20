namespace Listmate.Domain.Lists;

public enum MemberRole
{
    Owner,
    Editor,
}

public sealed class Membership(Guid listId, string userId, MemberRole role, DateTimeOffset joinedAt)
{
    public Guid ListId { get; private set; } = listId;

    public string UserId { get; private set; } = userId;

    public MemberRole Role { get; private set; } = role;

    public DateTimeOffset JoinedAt { get; private set; } = joinedAt;
}
