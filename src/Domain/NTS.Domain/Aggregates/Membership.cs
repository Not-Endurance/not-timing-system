using Not.Domain.Exceptions;

namespace NTS.Domain.Aggregates;

/// <summary>
/// An account's standing in one Tenant: the roles it holds there. Registering gives an account one in its home
/// Tenant, with no role; a grant adds another or a role (ADR-0012, #643). Profile edits never move the home Tenant.
/// </summary>
public sealed record Membership
{
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
}
