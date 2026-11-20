namespace Listmate.Application.Abstractions;

/// <summary>Another request committed a change to the same list (or the same mutation id) first.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The list does not exist or the caller is not a member; both look the same from outside.</summary>
public sealed class ListNotFoundException : Exception
{
    public ListNotFoundException()
        : base("List not found.")
    {
    }

    public ListNotFoundException(string message)
        : base(message)
    {
    }

    public ListNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The caller is a member but may not do this (only the owner removes people).</summary>
public sealed class ListPermissionException : Exception
{
    public ListPermissionException()
        : base("Only the list owner can do this.")
    {
    }

    public ListPermissionException(string message)
        : base(message)
    {
    }

    public ListPermissionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
