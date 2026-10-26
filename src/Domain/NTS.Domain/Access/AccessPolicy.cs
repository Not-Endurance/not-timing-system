namespace NTS.Domain.Access;

/// <summary>
/// The access matrix of ADR-0012 as one pure function: whether who stands at a place may do an action there, at the
/// stage the place is in. Every route and the capabilities answers ask this and nothing else, so what a screen offers
/// and what the server accepts cannot differ.
/// </summary>
public static class AccessPolicy
{
    static readonly EventStage[] ANY_STAGE = [EventStage.Unstarted, EventStage.Live, EventStage.Historic];
    static readonly EventStage[] BEFORE_THE_END = [EventStage.Unstarted, EventStage.Live];
    static readonly EventStage[] UNSTARTED = [EventStage.Unstarted];
    static readonly EventStage[] LIVE = [EventStage.Live];
    static readonly EventStage[] NO_STAGE = [];

    public static Verdict Decide(Capability capability, Standing standing, PlaceState state)
    {
        var requirement = RequirementOf(capability);
        if (requirement.Level == AccessLevel.Anonymous)
        {
            return Verdict.Allowed;
        }

        // An action is asked about in the place it belongs to: asking about it anywhere else is a mistake of the caller of
        // the policy and not a refusal.
        if (requirement.Place != state.Place)
        {
            throw new ArgumentException($"{capability} is not an action of the place '{state.Place}'.", nameof(state));
        }

        if (standing.Level == AccessLevel.Anonymous)
        {
            return Verdict.Refused(Refusal.NotSignedIn);
        }

        if (!Holds(standing, requirement))
        {
            return Verdict.Refused(MissingLevel(requirement));
        }

        if (requirement.NeedsOperationalTenant && !state.IsOperational)
        {
            return Verdict.Refused(Refusal.TenantNotOperational);
        }

        if (requirement.Place == Place.Event && !requirement.Stages.Contains(state.Stage))
        {
            return Verdict.Refused(MissingStage(state.Stage));
        }

        return Verdict.Allowed;
    }

    public static Requirement RequirementOf(Capability capability)
    {
        return capability switch
        {
            Capability.ReadPublicViews => new Requirement(Place.Platform, AccessLevel.Anonymous, NO_STAGE),
            Capability.SignedIn => new Requirement(Place.Platform, AccessLevel.Account, NO_STAGE),
            Capability.SeeTimeEvents => new Requirement(Place.Event, AccessLevel.Staff, ANY_STAGE),
            Capability.SendSnapshot => new Requirement(Place.Event, AccessLevel.Staff, LIVE, mustRecord: true),
            Capability.ConfigureEvent => new Requirement(Place.Event, AccessLevel.ConsoleOperator, BEFORE_THE_END),
            Capability.EditSetup => new Requirement(Place.Event, AccessLevel.ConsoleOperator, UNSTARTED),
            Capability.ReadConfiguration => new Requirement(Place.Event, AccessLevel.ConsoleOperator, ANY_STAGE),
            Capability.RunEvent => new Requirement(Place.Event, AccessLevel.ConsoleOperator, LIVE),
            Capability.AssignConsoleOperators => new Requirement(Place.Event, AccessLevel.Root, BEFORE_THE_END),
            Capability.DeleteEvent => new Requirement(Place.Event, AccessLevel.Root, UNSTARTED),
            Capability.CreateEvent => new Requirement(
                Place.Tenant,
                AccessLevel.Root,
                NO_STAGE,
                needsOperationalTenant: true
            ),
            Capability.EditTenant => new Requirement(Place.Tenant, AccessLevel.Root, NO_STAGE),
            Capability.EditRegistry => new Requirement(Place.Tenant, AccessLevel.ConsoleOperator, NO_STAGE),
            Capability.SetUpAcrossTenants => new Requirement(Place.Platform, AccessLevel.ConsoleOperator, NO_STAGE),
            Capability.EditCountries => new Requirement(Place.Platform, AccessLevel.Root, NO_STAGE),
            _ => throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                "A row the matrix does not have."
            ),
        };
    }

    static bool Holds(Standing standing, Requirement requirement)
    {
        return standing.Level >= requirement.Level && (!requirement.MustRecord || standing.MayRecord);
    }

    /// <summary>The refusal that names the Level a row needs: the one a person who lacks it is told. At the platform Root is the Developer.</summary>
    static Refusal MissingLevel(Requirement requirement)
    {
        return requirement.Level switch
        {
            AccessLevel.ConsoleOperator => Refusal.NotConsoleOperator,
            AccessLevel.Root => requirement.Place == Place.Platform ? Refusal.NotDeveloper : Refusal.NotRoot,
            _ => Refusal.NotAllowed,
        };
    }

    static Refusal MissingStage(EventStage stage)
    {
        return stage switch
        {
            EventStage.Unstarted => Refusal.EventNotStarted,
            EventStage.Live => Refusal.EventStarted,
            _ => Refusal.EventEnded,
        };
    }
}
