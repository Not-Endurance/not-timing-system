using NTS.Domain.Access;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Access;

/// <summary>
/// The access matrix of ADR-0012, row by row: for every action, who holds it before the Event starts, while it is Live
/// and once it is Historic. What each row expects is written out here from the ADR and the glossary and is never
/// computed by the policy it checks, so the table is the specification and a change to the policy shows up as a row
/// that no longer holds.
/// </summary>
public sealed class AccessPolicyTests
{
    public static TheoryData<Capability, EventStage> EventActions()
    {
        var rows = new TheoryData<Capability, EventStage>();
        foreach (var capability in EventCapabilities())
        {
            foreach (var stage in Enum.GetValues<EventStage>())
            {
                rows.Add(capability, stage);
            }
        }

        return rows;
    }

    public static TheoryData<Capability> TenantActions()
    {
        var rows = new TheoryData<Capability>();
        foreach (var capability in TenantRows().Keys)
        {
            rows.Add(capability);
        }

        return rows;
    }

    public static TheoryData<Capability> PlatformActions()
    {
        var rows = new TheoryData<Capability>();
        foreach (var capability in PlatformRows().Keys)
        {
            rows.Add(capability);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(TenantActions))]
    public void An_action_of_a_Tenant_is_allowed_to_the_people_the_matrix_names(Capability capability)
    {
        var allowed = Everybody()
            .Where(x => AccessPolicy.Decide(capability, StandingOf(x), PlaceState.OfTenant(true)).IsAllowed);

        Assert.Equal(TenantRows()[capability].Order(), allowed.Order());
    }

    [Theory]
    [MemberData(nameof(PlatformActions))]
    public void An_action_of_the_platform_is_allowed_to_the_people_the_matrix_names(Capability capability)
    {
        var allowed = Everybody()
            .Where(x => AccessPolicy.Decide(capability, StandingOf(x), PlaceState.Platform).IsAllowed);

        Assert.Equal(PlatformRows()[capability].Order(), allowed.Order());
    }

    [Fact]
    public void Every_row_of_the_matrix_has_a_row_in_this_table()
    {
        var covered = EventCapabilities().Concat(TenantRows().Keys).Concat(PlatformRows().Keys);

        Assert.Equal(Enum.GetValues<Capability>().Order(), covered.Order());
    }

    [Theory]
    [MemberData(nameof(EventActions))]
    public void An_action_of_an_Event_is_allowed_to_the_people_the_matrix_names_at_each_stage(
        Capability capability,
        EventStage stage
    )
    {
        var expected = EventRows().GetValueOrDefault((capability, stage)) ?? [];

        var allowed = Everybody()
            .Where(x => AccessPolicy.Decide(capability, StandingOf(x), PlaceState.OfEvent(stage)).IsAllowed);

        Assert.Equal(expected.Order(), allowed.Order());
    }

    [Theory]
    [InlineData(Who.Anonymous, Capability.SendSnapshot, EventStage.Live, Refusal.NotSignedIn)]
    [InlineData(Who.Anonymous, Capability.ConfigureEvent, EventStage.Unstarted, Refusal.NotSignedIn)]
    [InlineData(Who.Anonymous, Capability.RunEvent, EventStage.Unstarted, Refusal.NotSignedIn)]
    [InlineData(Who.Account, Capability.SendSnapshot, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.StaffWhoMayNotRecord, Capability.SendSnapshot, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.Account, Capability.SeeTimeEvents, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.StaffWhoMayRecord, Capability.ConfigureEvent, EventStage.Unstarted, Refusal.NotConsoleOperator)]
    [InlineData(Who.Account, Capability.EditSetup, EventStage.Unstarted, Refusal.NotConsoleOperator)]
    [InlineData(Who.StaffWhoMayRecord, Capability.RunEvent, EventStage.Live, Refusal.NotConsoleOperator)]
    [InlineData(Who.Account, Capability.ReadConfiguration, EventStage.Live, Refusal.NotConsoleOperator)]
    [InlineData(Who.ConsoleOperator, Capability.AssignConsoleOperators, EventStage.Unstarted, Refusal.NotRoot)]
    [InlineData(Who.ConsoleOperator, Capability.AssignConsoleOperators, EventStage.Live, Refusal.NotRoot)]
    [InlineData(Who.ConsoleOperator, Capability.DeleteEvent, EventStage.Unstarted, Refusal.NotRoot)]
    [InlineData(Who.ConsoleOperator, Capability.AssignConsoleOperators, EventStage.Historic, Refusal.NotRoot)]
    [InlineData(Who.Account, Capability.RunEvent, EventStage.Historic, Refusal.NotConsoleOperator)]
    [InlineData(Who.Root, Capability.DeleteEvent, EventStage.Live, Refusal.EventStarted)]
    [InlineData(Who.Root, Capability.DeleteEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.Root, Capability.AssignConsoleOperators, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.Root, Capability.RunEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.ConsoleOperator, Capability.RunEvent, EventStage.Unstarted, Refusal.EventNotStarted)]
    [InlineData(Who.ConsoleOperator, Capability.RunEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.ConsoleOperator, Capability.EditSetup, EventStage.Live, Refusal.EventStarted)]
    [InlineData(Who.ConsoleOperator, Capability.EditSetup, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.ConsoleOperator, Capability.ConfigureEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.StaffWhoMayRecord, Capability.SendSnapshot, EventStage.Unstarted, Refusal.EventNotStarted)]
    [InlineData(Who.StaffWhoMayRecord, Capability.SendSnapshot, EventStage.Historic, Refusal.EventEnded)]
    public void A_refusal_says_what_is_missing_the_Level_before_the_stage(
        Who who,
        Capability capability,
        EventStage stage,
        Refusal expected
    )
    {
        var verdict = AccessPolicy.Decide(capability, StandingOf(who), PlaceState.OfEvent(stage));

        Assert.False(verdict.IsAllowed);
        Assert.Equal(expected, verdict.Reason);
    }

    [Theory]
    [InlineData(Who.Anonymous, Capability.CreateEvent, Refusal.NotSignedIn)]
    [InlineData(Who.Account, Capability.CreateEvent, Refusal.NotRoot)]
    [InlineData(Who.ConsoleOperator, Capability.CreateEvent, Refusal.NotRoot)]
    [InlineData(Who.Account, Capability.EditTenant, Refusal.NotRoot)]
    [InlineData(Who.ConsoleOperator, Capability.EditTenant, Refusal.NotRoot)]
    [InlineData(Who.Account, Capability.EditRegistry, Refusal.NotConsoleOperator)]
    [InlineData(Who.StaffWhoMayRecord, Capability.EditRegistry, Refusal.NotConsoleOperator)]
    public void A_refusal_of_an_action_of_a_Tenant_says_what_is_missing(
        Who who,
        Capability capability,
        Refusal expected
    )
    {
        var verdict = AccessPolicy.Decide(capability, StandingOf(who), PlaceState.OfTenant(true));

        Assert.False(verdict.IsAllowed);
        Assert.Equal(expected, verdict.Reason);
    }

    [Theory]
    [InlineData(Who.Anonymous, Capability.SignedIn, Refusal.NotSignedIn)]
    [InlineData(Who.Anonymous, Capability.SetUpAcrossTenants, Refusal.NotSignedIn)]
    [InlineData(Who.Account, Capability.SetUpAcrossTenants, Refusal.NotConsoleOperator)]
    [InlineData(Who.Anonymous, Capability.EditCountries, Refusal.NotSignedIn)]
    [InlineData(Who.Account, Capability.EditCountries, Refusal.NotDeveloper)]
    [InlineData(Who.ConsoleOperator, Capability.EditCountries, Refusal.NotDeveloper)]
    public void A_refusal_of_an_action_of_the_platform_says_what_is_missing(
        Who who,
        Capability capability,
        Refusal expected
    )
    {
        var verdict = AccessPolicy.Decide(capability, StandingOf(who), PlaceState.Platform);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(expected, verdict.Reason);
    }

    [Fact]
    public void An_Event_is_not_created_in_a_Tenant_that_is_not_operational_by_anybody_who_may_create_one()
    {
        var notOperational = PlaceState.OfTenant(false);

        var root = AccessPolicy.Decide(Capability.CreateEvent, StandingOf(Who.Root), notOperational);
        var account = AccessPolicy.Decide(Capability.CreateEvent, StandingOf(Who.Account), notOperational);

        Assert.Equal(Refusal.TenantNotOperational, root.Reason);
        Assert.Equal(Refusal.NotRoot, account.Reason);
    }

    [Fact]
    public void Public_views_are_allowed_to_everybody_in_any_place_and_say_nothing_when_they_are()
    {
        var verdict = AccessPolicy.Decide(
            Capability.ReadPublicViews,
            Standing.Anonymous,
            PlaceState.OfEvent(EventStage.Live)
        );

        Assert.True(verdict.IsAllowed);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void An_action_is_asked_about_in_the_place_it_belongs_to()
    {
        var account = StandingOf(Who.Account);

        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.ConfigureEvent, account, PlaceState.OfTenant(true))
        );
        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.CreateEvent, account, PlaceState.Platform)
        );
        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.EditCountries, account, PlaceState.OfEvent(EventStage.Live))
        );
    }

    [Fact]
    public void An_action_asked_about_in_the_wrong_place_is_a_mistake_even_when_nobody_is_signed_in()
    {
        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.ConfigureEvent, Standing.Anonymous, PlaceState.Platform)
        );
    }

    [Fact]
    public void A_row_the_matrix_does_not_have_is_a_mistake()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccessPolicy.Decide((Capability)99, StandingOf(Who.Root), PlaceState.Platform)
        );
    }

    static Standing StandingOf(Who who)
    {
        return who switch
        {
            Who.Anonymous => Standing.Anonymous,
            Who.Account => Standing.Of(AccessLevel.Account),
            Who.StaffWhoMayNotRecord => Standing.Of(AccessLevel.Staff, mayRecord: false),
            Who.StaffWhoMayRecord => Standing.Of(AccessLevel.Staff, mayRecord: true),
            Who.ConsoleOperator => Standing.Of(AccessLevel.ConsoleOperator),
            Who.Root => Standing.Of(AccessLevel.Root),
            _ => throw new ArgumentOutOfRangeException(nameof(who), who, "A person the table does not know."),
        };
    }

    static Who[] Everybody()
    {
        return [.. Enum.GetValues<Who>()];
    }

    static Capability[] EventCapabilities()
    {
        return
        [
            Capability.SeeTimeEvents,
            Capability.SendSnapshot,
            Capability.ConfigureEvent,
            Capability.EditSetup,
            Capability.ReadConfiguration,
            Capability.RunEvent,
            Capability.AssignConsoleOperators,
            Capability.DeleteEvent,
        ];
    }

    /// <summary>
    /// What ADR-0012 says about each action of an Event, at each stage of it. An action that is missing at a stage is
    /// allowed to nobody at that stage.
    /// </summary>
    static Dictionary<(Capability, EventStage), Who[]> EventRows()
    {
        Who[] staff = [Who.StaffWhoMayNotRecord, Who.StaffWhoMayRecord, Who.ConsoleOperator, Who.Root];
        Who[] whoMayRecord = [Who.StaffWhoMayRecord, Who.ConsoleOperator, Who.Root];
        Who[] whoWorkTheConsole = [Who.ConsoleOperator, Who.Root];
        Who[] rootAlone = [Who.Root];
        return new()
        {
            // "See how times were recorded: Staff", and Root holds everything below it.
            [(Capability.SeeTimeEvents, EventStage.Unstarted)] = staff,
            [(Capability.SeeTimeEvents, EventStage.Live)] = staff,
            [(Capability.SeeTimeEvents, EventStage.Historic)] = staff,
            // "Send a Snapshot: Staff whose grant may record", while the Event is Live.
            [(Capability.SendSnapshot, EventStage.Live)] = whoMayRecord,
            // "Configure an Event: Console Operator", until the Event ends.
            [(Capability.ConfigureEvent, EventStage.Unstarted)] = whoWorkTheConsole,
            [(Capability.ConfigureEvent, EventStage.Live)] = whoWorkTheConsole,
            // The Setup is what the Event is configured with before it starts, and the Console works on copies after.
            [(Capability.EditSetup, EventStage.Unstarted)] = whoWorkTheConsole,
            // "Read the Setup and who has access: Console Operator", at every stage.
            [(Capability.ReadConfiguration, EventStage.Unstarted)] = whoWorkTheConsole,
            [(Capability.ReadConfiguration, EventStage.Live)] = whoWorkTheConsole,
            [(Capability.ReadConfiguration, EventStage.Historic)] = whoWorkTheConsole,
            // "Run the Event: edit what it keeps, reset it", only while it is Live.
            [(Capability.RunEvent, EventStage.Live)] = whoWorkTheConsole,
            // "Assign and remove Console Operators: Root", until the Event ends.
            [(Capability.AssignConsoleOperators, EventStage.Unstarted)] = rootAlone,
            [(Capability.AssignConsoleOperators, EventStage.Live)] = rootAlone,
            // "Delete an Event: Root", before it starts.
            [(Capability.DeleteEvent, EventStage.Unstarted)] = rootAlone,
        };
    }

    /// <summary>The actions of one Tenant, whatever the stage of its Events.</summary>
    static Dictionary<Capability, Who[]> TenantRows()
    {
        return new()
        {
            // "Create an Event: Root".
            [Capability.CreateEvent] = [Who.Root],
            // "Tenant rules and Tenant branding: Root".
            [Capability.EditTenant] = [Who.Root],
            // "Edit the Tenant's registry, search its accounts by name: Console Operator", which is one of any Event of the Tenant
            // that has not ended, and above that Root.
            [Capability.EditRegistry] = [Who.ConsoleOperator, Who.Root],
        };
    }

    /// <summary>The actions that belong to no Tenant and no Event.</summary>
    static Dictionary<Capability, Who[]> PlatformRows()
    {
        return new()
        {
            // "Read public Event views: Anonymous", which is anybody.
            [Capability.ReadPublicViews] = Everybody(),
            // What an account does for itself or reads of the registries: any signed-in account.
            [Capability.SignedIn] =
            [
                Who.Account,
                Who.StaffWhoMayNotRecord,
                Who.StaffWhoMayRecord,
                Who.ConsoleOperator,
                Who.Root,
            ],
            // The whole row of a rider of another Tenant, a search that matches the FEI ID, the account search across Tenants:
            // the people who set an Event up.
            [Capability.SetUpAcrossTenants] = [Who.ConsoleOperator, Who.Root],
            // The countries are the platform's reference data, which the owner of the platform keeps.
            [Capability.EditCountries] = [Who.Root],
        };
    }

    public enum Who
    {
        Anonymous,
        Account,
        StaffWhoMayNotRecord,
        StaffWhoMayRecord,
        ConsoleOperator,
        Root,
    }
}
