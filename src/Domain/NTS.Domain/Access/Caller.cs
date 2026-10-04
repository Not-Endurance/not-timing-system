namespace NTS.Domain.Access;

/// <summary>
/// Who asks, as the policy needs to know them: the account (none when nobody is signed in), whether it is the
/// Developer, the Tenants it is a Tenant Root of, and the Tenants of the Events it is the Main Operator of that are not
/// yet Historic. What an account has been granted on one Event is the grants of the scope, not of the caller.
/// </summary>
public sealed class Caller
{
    public static Caller Of(
        Guid accountId,
        bool isDeveloper = false,
        IEnumerable<string>? tenantRootIn = null,
        IEnumerable<string>? openMainOperatorIn = null
    )
    {
        return new Caller(accountId, isDeveloper, tenantRootIn ?? [], openMainOperatorIn ?? []);
    }

    Caller(Guid? accountId, bool isDeveloper, IEnumerable<string> tenantRootIn, IEnumerable<string> openMainOperatorIn)
    {
        AccountId = accountId;
        IsDeveloper = isDeveloper;
        TenantRootIn = new HashSet<string>(tenantRootIn, StringComparer.Ordinal);
        OpenMainOperatorIn = new HashSet<string>(openMainOperatorIn, StringComparer.Ordinal);
    }

    public static Caller Anonymous { get; } = new(null, false, [], []);

    public Guid? AccountId { get; }
    public bool IsSignedIn => AccountId != null;
    public bool IsDeveloper { get; }

    /// <summary>The Tenants in which the account holds the Tenant Root role.</summary>
    public IReadOnlySet<string> TenantRootIn { get; }

    /// <summary>The Tenants of the Events, not yet Historic, that the account is the Main Operator of.</summary>
    public IReadOnlySet<string> OpenMainOperatorIn { get; }
}
