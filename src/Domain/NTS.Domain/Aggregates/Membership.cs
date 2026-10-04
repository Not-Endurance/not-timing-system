using Not.Domain.Exceptions;

namespace NTS.Domain.Aggregates;

/// <summary>
/// An account's standing in one Tenant: the roles it holds there. Registering gives an account one in its home
/// Tenant, with no role; a grant adds another or a role (ADR-0012, #643). Profile edits never move the home Tenant.
/// </summary>
public sealed record Membership
{
    /// <summary>The role of a Tenant Root: it creates the Tenant's Events. Only the Developer's command gives it.</summary>
    public const string TENANT_ROOT = "tenant-root";

    public static Membership In(Tenant tenant)
    {
        return new Membership(tenant.Id);
    }

    public Membership(string tenantId, IEnumerable<string>? roles = null)
    {
        TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? throw new DomainPropertyException(
                nameof(TenantId),
                string.Format(Field_1_is_required_on_2_string, nameof(TenantId), nameof(Membership))
            )
            : tenantId;
        Roles = [.. roles ?? []];
    }

    public string TenantId { get; }
    public IReadOnlyList<string> Roles { get; }
    public bool IsTenantRoot => Roles.Contains(TENANT_ROOT);

    /// <summary>The Membership with the role as well. Having it already changes nothing.</summary>
    public Membership WithRole(string role)
    {
        return Roles.Contains(role) ? this : new Membership(TenantId, [.. Roles, role]);
    }
}
