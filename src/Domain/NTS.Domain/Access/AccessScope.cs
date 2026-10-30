namespace NTS.Domain.Access;

/// <summary>The kinds of place an action is done in.</summary>
public enum ScopeKind
{
    Platform = 0,
    Tenant = 1,
    Event = 2,
}

/// <summary>
/// Where an action is done, with what the policy needs to know about the place: the platform, a Tenant (and whether it
/// is operational), or an Event (its Tenant, its one Main Operator, its stage, and what the caller has been granted on
/// it). The server reads these facts; the policy only reasons over them.
/// </summary>
public sealed class AccessScope
{
    public static AccessScope ForTenant(string tenantId, bool isOperational)
    {
        return new AccessScope(
            ScopeKind.Tenant,
            Required(tenantId),
            isOperational,
            null,
            EventStage.Unstarted,
            CallerGrants.None
        );
    }

    public static AccessScope ForEvent(
        string tenantId,
        Guid? mainOperatorId,
        EventStage stage,
        CallerGrants? callerGrants = null
    )
    {
        return new AccessScope(
            ScopeKind.Event,
            Required(tenantId),
            true,
            mainOperatorId,
            stage,
            callerGrants ?? CallerGrants.None
        );
    }

    AccessScope(
        ScopeKind kind,
        string? tenantId,
        bool isOperational,
        Guid? mainOperatorId,
        EventStage stage,
        CallerGrants callerGrants
    )
    {
        Kind = kind;
        TenantId = tenantId;
        IsOperational = isOperational;
        MainOperatorId = mainOperatorId;
        Stage = stage;
        CallerGrants = callerGrants;
    }

    public static AccessScope Platform { get; } =
        new(ScopeKind.Platform, null, true, null, EventStage.Unstarted, CallerGrants.None);

    public ScopeKind Kind { get; }

    /// <summary>The Tenant of the Tenant or the Event; none for the platform.</summary>
    public string? TenantId { get; }

    /// <summary>Whether the Tenant can hold Events, which it can once it has a Tenant Root.</summary>
    public bool IsOperational { get; }

    /// <summary>
    /// The account that is the Event's Main Operator; none for an Event that has none, such as a Historic one from before
    /// Tenants.
    /// </summary>
    public Guid? MainOperatorId { get; }

    public EventStage Stage { get; }
    public CallerGrants CallerGrants { get; }

    static string Required(string tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId)
            ? throw new ArgumentException("A Tenant is needed to say where the action is done.", nameof(tenantId))
            : tenantId;
    }
}
