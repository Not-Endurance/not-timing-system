using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NTS.Contracts.Features.Console.Setup;

/// <summary>
/// The people the Console Operator links to the Officials and Operators of its Event (#646, ADR-0012): by the exact email, which
/// gives an account the access at once, or, for an address that has no account, a pending invitation that attaches when the
/// person registers. An address is never given back whole.
/// </summary>
public interface IConsoleGrants
{
    Task<IReadOnlyList<GrantRow>> List(Guid eventId);

    /// <summary>Links the person whose email it is as an Operator, or as an Official of the role.</summary>
    Task<GrantRow> Link(Guid eventId, GrantKind kind, OfficialRole? role, string email);

    /// <summary>Ends the access of a grant.</summary>
    Task Remove(Guid grantId);
}

/// <summary>One grant of an Event as the Api tells it.</summary>
public sealed class GrantRow
{
    public required Guid Id { get; init; }
    public required GrantKind Kind { get; init; }
    public OfficialRole? Role { get; init; }

    /// <summary>The invitation waits for a person who has no account: it gives nothing until they register.</summary>
    public required bool IsPending { get; init; }

    /// <summary>The name of the account, when there is one.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The address as the Api masks it (<c>a***@example.test</c>).</summary>
    public required string Email { get; init; }
}
