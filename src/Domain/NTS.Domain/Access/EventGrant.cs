using NTS.Domain.Enums;

namespace NTS.Domain.Access;

/// <summary>What a grant is for: Snapshot access as an Operator, or a link to an Official of the Event (ADR-0012).</summary>
public enum GrantKind
{
    Operator = 1,
    Official = 2,
}

/// <summary>
/// The access the Main Operator gives to a person for one Event: as an Operator (never named in documents) or as an
/// Official of a role (named in documents). A grant names the person by their exact email. When the email has an account
/// the grant is theirs at once; when it has none it is a pending invitation that attaches to the account that
/// registers with that email, and gives nothing until then. A grant never moves from one account to another.
/// </summary>
public sealed record EventGrant
{
    public static EventGrant ForOperator(Guid id, Guid eventId, string tenantId, string email, Guid? accountId = null)
    {
        return new EventGrant(id, eventId, tenantId, GrantKind.Operator, null, NormalizeEmail(email), accountId);
    }

    public static EventGrant ForOfficial(
        Guid id,
        Guid eventId,
        string tenantId,
        OfficialRole role,
        string email,
        Guid? accountId = null
    )
    {
        return new EventGrant(id, eventId, tenantId, GrantKind.Official, role, NormalizeEmail(email), accountId);
    }

    /// <summary>The email as grants and accounts are matched by it: without the spaces around it, in lower case.</summary>
    public static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("A grant names an email.", nameof(email))
            : email.Trim().ToLowerInvariant();
    }

    EventGrant(
        Guid id,
        Guid eventId,
        string tenantId,
        GrantKind kind,
        OfficialRole? officialRole,
        string email,
        Guid? accountId
    )
    {
        Id = id;
        EventId = eventId;
        TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? throw new ArgumentException("A grant belongs to a Tenant.", nameof(tenantId))
            : tenantId;
        Kind = kind;
        OfficialRole = officialRole;
        Email = email;
        AccountId = accountId;
    }

    public Guid Id { get; }
    public Guid EventId { get; }
    public string TenantId { get; }
    public GrantKind Kind { get; }

    /// <summary>The role of the Official the person is linked to; none for an Operator.</summary>
    public OfficialRole? OfficialRole { get; }

    public string Email { get; }

    /// <summary>The account the grant is theirs through; none while the invitation waits for the person to register.</summary>
    public Guid? AccountId { get; private init; }

    public bool IsPending => AccountId == null;

    /// <summary>The grant once the person it was made for has an account. Another account cannot take it over.</summary>
    public EventGrant AttachedTo(Guid accountId)
    {
        if (AccountId == accountId)
        {
            return this;
        }

        return AccountId != null
            ? throw new InvalidOperationException("A grant that belongs to an account does not move to another.")
            : this with
            {
                AccountId = accountId,
            };
    }
}
