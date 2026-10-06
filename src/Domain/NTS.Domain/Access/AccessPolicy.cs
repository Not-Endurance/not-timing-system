using NTS.Domain.Enums;

namespace NTS.Domain.Access;

/// <summary>
/// The access matrix of ADR-0012 as one pure function: for a caller and the place an action is done in, whether the
/// action is allowed and, if it is not, what is missing. Every write route and the capabilities endpoint ask this and
/// nothing else, so what a screen offers and what the server accepts cannot differ.
/// <para>
/// A role is checked before the stage of the Event, so a person who may not do an action is only told that. The
/// Developer holds every right across Tenants except acting as the Main Operator of a Live Event: what the Main
/// Operator does there is the Main Operator's alone, and a Live Event whose Main Operator is locked out is a database
/// fix and not an action of the product. Being the Main Operator by appointment is a role like any other, whoever holds
/// it.
/// </para>
/// </summary>
public static class AccessPolicy
{
    static readonly EventStage[] BEFORE_THE_END = [EventStage.Unstarted, EventStage.Live];
    static readonly EventStage[] ANY_STAGE = [EventStage.Unstarted, EventStage.Live, EventStage.Historic];
    static readonly EventStage[] UNSTARTED = [EventStage.Unstarted];
    static readonly EventStage[] LIVE = [EventStage.Live];

    public static Verdict Decide(Capability capability, Caller caller, AccessScope scope)
    {
        RequireScope(capability, scope);
        if (capability == Capability.ReadPublicViews)
        {
            return Verdict.Allowed;
        }

        if (!caller.IsSignedIn)
        {
            return Verdict.Refused(Refusal.NotSignedIn);
        }

        return scope.Kind switch
        {
            ScopeKind.Event => DecideForEvent(capability, caller, scope),
            ScopeKind.Tenant => DecideForTenant(capability, caller, scope),
            _ => DecideForPlatform(capability, caller),
        };
    }

    /// <summary>Whether the caller is the one Main Operator of the Event. An Event that has none has nobody.</summary>
    public static bool IsMainOperator(Caller caller, AccessScope scope)
    {
        return scope.Kind == ScopeKind.Event
            && caller.AccountId is { } account
            && scope.MainOperatorId is { } mainOperator
            && account == mainOperator;
    }

    /// <summary>
    /// The roles of an Official that may send a Snapshot. Any Operator may, and so may the Main Operator; the other
    /// Officials, such as the veterinary commission, are Staff who see how times were recorded but do not record them.
    /// </summary>
    public static IReadOnlyList<OfficialRole> SnapshotOfficialRoles { get; } =
        [OfficialRole.Steward, OfficialRole.ChiefSteward, OfficialRole.GroundJury, OfficialRole.GroundJuryPresident];

    static Verdict DecideForEvent(Capability capability, Caller caller, AccessScope scope)
    {
        var isMainOperator = IsMainOperator(caller, scope);
        var isTenantRoot = caller.TenantRootIn.Contains(scope.TenantId!);
        var developerMayAct = caller.IsDeveloper && scope.Stage != EventStage.Live;

        return capability switch
        {
            Capability.SeeTimeEvents => Check(
                isMainOperator || scope.CallerGrants.IsStaff || caller.IsDeveloper,
                Refusal.NotAllowed,
                scope,
                ANY_STAGE
            ),
            Capability.SendSnapshot => Check(
                isMainOperator || scope.CallerGrants.MaySendSnapshot,
                Refusal.NotAllowed,
                scope,
                LIVE
            ),
            Capability.ConfigureEvent or Capability.LinkAccounts or Capability.EditEventBranding => Check(
                isMainOperator || developerMayAct,
                Refusal.NotMainOperator,
                scope,
                BEFORE_THE_END
            ),
            Capability.EditSetup => Check(isMainOperator || developerMayAct, Refusal.NotMainOperator, scope, UNSTARTED),
            Capability.EditEventData => Check(isMainOperator || developerMayAct, Refusal.NotMainOperator, scope, LIVE),
            Capability.ReadSetup => Check(
                isMainOperator || isTenantRoot || caller.IsDeveloper,
                Refusal.NotAllowed,
                scope,
                ANY_STAGE
            ),
            Capability.AssignMainOperator or Capability.DeleteEvent => Check(
                isTenantRoot || caller.IsDeveloper,
                Refusal.NotTenantRoot,
                scope,
                UNSTARTED
            ),
            Capability.HandOverMainOperator or Capability.ResetEvent => Check(
                isMainOperator,
                Refusal.NotMainOperator,
                scope,
                LIVE
            ),
            _ => throw NotAnActionOf(capability, ScopeKind.Event),
        };
    }

    static Verdict DecideForTenant(Capability capability, Caller caller, AccessScope scope)
    {
        var tenantId = scope.TenantId!;
        var isTenantRoot = caller.TenantRootIn.Contains(tenantId);
        var hasAuthority = isTenantRoot || caller.OpenMainOperatorIn.Contains(tenantId);

        return capability switch
        {
            Capability.CreateEvent => CreateEvent(isTenantRoot || caller.IsDeveloper, scope),
            Capability.EditTenantRules or Capability.EditTenantBranding => Role(
                isTenantRoot || caller.IsDeveloper,
                Refusal.NotTenantRoot
            ),
            Capability.EditRegistry or Capability.SearchAccounts => Role(
                hasAuthority || caller.IsDeveloper,
                Refusal.NotAllowed
            ),
            _ => throw NotAnActionOf(capability, ScopeKind.Tenant),
        };
    }

    static Verdict DecideForPlatform(Capability capability, Caller caller)
    {
        return capability switch
        {
            Capability.ReadRegistryAcrossTenants => Verdict.Allowed,
            Capability.SearchAccountsAcrossTenants => Role(
                caller.TenantRootIn.Count > 0 || caller.OpenMainOperatorIn.Count > 0 || caller.IsDeveloper,
                Refusal.NotAllowed
            ),
            Capability.SeedTenantRoot or Capability.GrantDeveloper or Capability.EditCountries => Role(
                caller.IsDeveloper,
                Refusal.NotDeveloper
            ),
            _ => throw NotAnActionOf(capability, ScopeKind.Platform),
        };
    }

    static Verdict CreateEvent(bool mayCreate, AccessScope scope)
    {
        if (!mayCreate)
        {
            return Verdict.Refused(Refusal.NotTenantRoot);
        }

        return scope.IsOperational ? Verdict.Allowed : Verdict.Refused(Refusal.TenantNotOperational);
    }

    static Verdict Role(bool holds, Refusal ifNot)
    {
        return holds ? Verdict.Allowed : Verdict.Refused(ifNot);
    }

    /// <summary>The role first, then the stage of the Event: an Event that is not at a stage that takes the action refuses it.</summary>
    static Verdict Check(bool holdsRole, Refusal ifNot, AccessScope scope, EventStage[] stages)
    {
        if (!holdsRole)
        {
            return Verdict.Refused(ifNot);
        }

        if (stages.Contains(scope.Stage))
        {
            return Verdict.Allowed;
        }

        return Verdict.Refused(
            scope.Stage switch
            {
                EventStage.Unstarted => Refusal.EventNotStarted,
                EventStage.Live => Refusal.EventStarted,
                _ => Refusal.EventEnded,
            }
        );
    }

    /// <summary>
    /// An action is asked about in the place it belongs to: a Snapshot is sent to an Event, an Event is created in a
    /// Tenant. Asking about it anywhere else is a mistake of the caller of the policy and not a refusal.
    /// </summary>
    static void RequireScope(Capability capability, AccessScope scope)
    {
        if (capability == Capability.ReadPublicViews)
        {
            return;
        }

        var expected = ScopeOf(capability);
        if (scope.Kind != expected)
        {
            throw NotAnActionOf(capability, scope.Kind);
        }
    }

    static ScopeKind ScopeOf(Capability capability)
    {
        return capability switch
        {
            Capability.SeeTimeEvents
            or Capability.SendSnapshot
            or Capability.ConfigureEvent
            or Capability.EditSetup
            or Capability.EditEventData
            or Capability.ReadSetup
            or Capability.AssignMainOperator
            or Capability.HandOverMainOperator
            or Capability.LinkAccounts
            or Capability.EditEventBranding
            or Capability.ResetEvent
            or Capability.DeleteEvent => ScopeKind.Event,
            Capability.CreateEvent
            or Capability.EditRegistry
            or Capability.EditTenantRules
            or Capability.EditTenantBranding
            or Capability.SearchAccounts => ScopeKind.Tenant,
            _ => ScopeKind.Platform,
        };
    }

    static ArgumentException NotAnActionOf(Capability capability, ScopeKind kind)
    {
        return new ArgumentException($"{capability} is not an action of the scope '{kind}'.", nameof(capability));
    }
}
