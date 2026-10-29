namespace NTS.Contracts.Features.Account;

/// <summary>
/// The account of the person signed in, as <c>GET /api/me</c> tells it (ADR-0012): who they are and where they stand in the
/// Tenants. The roles are what the Ui needs to decide what to offer; the host decides what is allowed.
/// </summary>
public sealed class CurrentAccount
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public bool EmailConfirmed { get; init; }
    public string? Name { get; init; }
    public int Passkeys { get; init; }

    /// <summary>Whether the profile names a first name, a surname and a country, which sending a Snapshot asks for (ADR-0001).</summary>
    public bool ProfileComplete { get; init; }

    public string? HomeTenantId { get; init; }
    public string? SelectedTenantId { get; init; }

    /// <summary>The Tenant that what the person reads and writes outside an Event belongs to: the selected one, or the home one.</summary>
    public string? CurrentTenantId { get; init; }

    public IReadOnlyList<AccountMembership> Memberships { get; init; } = [];
    public bool IsDeveloper { get; init; }
}

/// <summary>The standing of an account in one Tenant: the Tenant and the roles held there.</summary>
public sealed class AccountMembership
{
    public required string TenantId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
