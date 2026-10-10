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

    /// <summary>Whether the caller is the Main Operator of the Event: the one who reaches its Console.</summary>
    public required bool IsMainOperator { get; init; }

    public bool CanConfigure { get; init; }
    public bool CanEditSetup { get; init; }
    public bool CanEditData { get; init; }
    public bool CanLinkAccounts { get; init; }
    public bool CanEditBranding { get; init; }
    public bool CanReset { get; init; }
    public bool CanHandOver { get; init; }
    public bool CanAssignMainOperator { get; init; }
}

/// <summary>An account that was found by name, to be named the Main Operator: its id, the name it is shown by and its address, partly masked as the Api gives it.</summary>
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
