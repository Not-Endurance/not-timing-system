namespace NTS.Domain.Access;

/// <summary>What an account is apart from any place: whether it is the Developer and the Tenants it is a Tenant Root of.</summary>
public sealed class Roles
{
    public static Roles Of(bool isDeveloper = false, IEnumerable<string>? tenantRootIn = null)
    {
        return new Roles(isDeveloper, tenantRootIn ?? []);
    }

    Roles(bool isDeveloper, IEnumerable<string> tenantRootIn)
    {
        IsDeveloper = isDeveloper;
        TenantRootIn = new HashSet<string>(tenantRootIn, StringComparer.Ordinal);
    }

    public static Roles None { get; } = new(false, []);

    public bool IsDeveloper { get; }

    /// <summary>The Tenants in which the account holds the Tenant Root role.</summary>
    public IReadOnlySet<string> TenantRootIn { get; }

    /// <summary>Whether the account sets Events up whatever Events it runs: the Developer, or a Tenant Root of some Tenant.</summary>
    public bool SetsEventsUp => IsDeveloper || TenantRootIn.Count > 0;

    /// <summary>Whether the account is Root at the Tenant: the Developer, or a Tenant Root of it.</summary>
    public bool IsRootAt(string tenantId)
    {
        return IsDeveloper || TenantRootIn.Contains(tenantId);
    }
}
