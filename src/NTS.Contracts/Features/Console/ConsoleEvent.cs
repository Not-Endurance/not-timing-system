using NTS.Domain.Access;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// An Event of the Console's home (#646): its Setup, its stage and what the caller may do about it, which is the Api's
/// answer (<c>GET /api/events/{id}/capabilities</c>) and not something the Ui works out, so that what a button offers and
/// what the Api accepts cannot differ.
/// </summary>
public sealed class ConsoleEvent
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Location { get; init; }
    public required EventStage Stage { get; init; }

    /// <summary>Where the caller stands at the Event, which is the Api's answer.</summary>
    public required AccessLevel Level { get; init; }

    /// <summary>Whether the caller works the Console of the Event: a Console Operator of it, or Root.</summary>
    public bool RunsTheConsole => Level >= AccessLevel.ConsoleOperator;

    /// <summary>Whether the caller may configure the Event: start it, change it, brand it and link its Operators and Officials.</summary>
    public bool CanConfigure { get; init; }

    public bool CanEditSetup { get; init; }

    /// <summary>Whether the caller may run the Event: edit what it keeps, and reset it.</summary>
    public bool CanRun { get; init; }

    public bool CanAssignConsoleOperators { get; init; }
}

/// <summary>A Console Operator of an Event as the Api lists it: the grant, the name it is shown by (none while an invitation waits) and its address, partly masked.</summary>
public sealed class ConsoleOperatorRow
{
    public ConsoleOperatorRow(Guid id, string displayName, string email, bool isPending)
    {
        Id = id;
        DisplayName = displayName;
        Email = email;
        IsPending = isPending;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public string Email { get; }

    /// <summary>Whether the address has no account yet, so that the grant waits for the person to register.</summary>
    public bool IsPending { get; }
}

/// <summary>An account that was found by name, to be made a Console Operator: its id, the name it is shown by and its address, partly masked as the Api gives it.</summary>
public sealed class AccountMatch
{
    public AccountMatch(Guid id, string displayName, string email)
    {
        Id = id;
        DisplayName = displayName;
        Email = email;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public string Email { get; }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Email) ? DisplayName : $"{DisplayName} ({Email})";
    }
}
