using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The access matrix of ADR-0012, row by row: for every action, who may do it before the Event starts, while it is Live
/// and once it is Historic. What each row expects is written out here from the ADR and the glossary and is never
/// computed by the policy it checks, so the table is the specification and a change to the policy shows up as a row
/// that no longer holds.
/// </summary>
public sealed class AccessPolicyTests
{
    const string TENANT = "country-bg";
    const string OTHER_TENANT = "country-tr";
    static readonly Guid MAIN_OPERATOR_ID = TestId.Of(1);
    static readonly Guid TENANT_ROOT_ID = TestId.Of(2);

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
    [MemberData(nameof(EventActions))]
    public void An_action_of_an_Event_is_allowed_to_the_people_the_matrix_names_at_each_stage(
        Capability capability,
        EventStage stage
    )
    {
        var expected = EventRows().GetValueOrDefault((capability, stage)) ?? [];

        var allowed = People()
            .Where(x => AccessPolicy.Decide(capability, x.Caller, AboutTheEvent(stage, x)).IsAllowed)
            .Select(x => x.Who);

        Assert.Equal(expected.Order(), allowed.Order());
    }

    [Theory]
    [MemberData(nameof(TenantActions))]
    public void An_action_of_a_Tenant_is_allowed_to_the_people_the_matrix_names(Capability capability)
    {
        var allowed = People()
            .Where(x => AccessPolicy.Decide(capability, x.Caller, AccessScope.ForTenant(TENANT, true)).IsAllowed)
            .Select(x => x.Who);

        Assert.Equal(TenantRows()[capability].Order(), allowed.Order());
    }

    [Theory]
    [MemberData(nameof(PlatformActions))]
    public void An_action_of_the_platform_is_allowed_to_the_people_the_matrix_names(Capability capability)
    {
        var allowed = People()
            .Where(x => AccessPolicy.Decide(capability, x.Caller, AccessScope.Platform).IsAllowed)
            .Select(x => x.Who);

        Assert.Equal(PlatformRows()[capability].Order(), allowed.Order());
    }

    [Fact]
    public void Every_action_of_the_matrix_has_a_row_in_this_table()
    {
        var covered = EventCapabilities().Concat(TenantRows().Keys).Concat(PlatformRows().Keys);

        Assert.Equal(Enum.GetValues<Capability>().Order(), covered.Order());
    }

    [Theory]
    [InlineData(Capability.CreateEvent)]
    [InlineData(Capability.EditRegistry)]
    [InlineData(Capability.EditTenantRules)]
    [InlineData(Capability.EditTenantBranding)]
    [InlineData(Capability.SearchAccounts)]
    public void A_Tenant_Root_or_a_Main_Operator_of_one_Tenant_has_no_authority_in_another(Capability capability)
    {
        var tenantRoot = Caller.Of(TestId.Of(30), tenantRootIn: [TENANT]);
        var mainOperator = Caller.Of(TestId.Of(31), openMainOperatorIn: [TENANT]);
        var elsewhere = AccessScope.ForTenant(OTHER_TENANT, true);

        Assert.False(AccessPolicy.Decide(capability, tenantRoot, elsewhere).IsAllowed);
        Assert.False(AccessPolicy.Decide(capability, mainOperator, elsewhere).IsAllowed);
    }

    [Theory]
    [InlineData(Capability.ConfigureEvent, EventStage.Unstarted)]
    [InlineData(Capability.ConfigureEvent, EventStage.Live)]
    [InlineData(Capability.AssignMainOperator, EventStage.Unstarted)]
    [InlineData(Capability.LinkAccounts, EventStage.Live)]
    [InlineData(Capability.DeleteEvent, EventStage.Unstarted)]
    public void A_Tenant_Root_has_no_authority_over_an_Event_of_another_Tenant(Capability capability, EventStage stage)
    {
        var tenantRoot = Caller.Of(TestId.Of(32), tenantRootIn: [OTHER_TENANT]);
        var scope = AccessScope.ForEvent(TENANT, MAIN_OPERATOR_ID, stage);

        Assert.False(AccessPolicy.Decide(capability, tenantRoot, scope).IsAllowed);
    }

    [Theory]
    [InlineData(EventStage.Unstarted, Capability.AssignMainOperator, true)]
    [InlineData(EventStage.Unstarted, Capability.DeleteEvent, true)]
    [InlineData(EventStage.Unstarted, Capability.ConfigureEvent, true)]
    [InlineData(EventStage.Live, Capability.ConfigureEvent, true)]
    [InlineData(EventStage.Live, Capability.HandOverMainOperator, true)]
    [InlineData(EventStage.Live, Capability.SendSnapshot, true)]
    [InlineData(EventStage.Live, Capability.ResetEvent, true)]
    [InlineData(EventStage.Live, Capability.AssignMainOperator, false)]
    [InlineData(EventStage.Live, Capability.DeleteEvent, false)]
    [InlineData(EventStage.Historic, Capability.ConfigureEvent, false)]
    [InlineData(EventStage.Historic, Capability.HandOverMainOperator, false)]
    public void A_Tenant_Root_who_created_the_Event_holds_the_rights_of_both_roles(
        EventStage stage,
        Capability capability,
        bool allowed
    )
    {
        var creator = Caller.Of(TENANT_ROOT_ID, tenantRootIn: [TENANT], openMainOperatorIn: [TENANT]);
        var scope = AccessScope.ForEvent(TENANT, TENANT_ROOT_ID, stage);

        Assert.Equal(allowed, AccessPolicy.Decide(capability, creator, scope).IsAllowed);
    }

    [Theory]
    [InlineData(EventStage.Unstarted, true)]
    [InlineData(EventStage.Live, true)]
    [InlineData(EventStage.Historic, false)]
    public void A_Developer_who_was_appointed_Main_Operator_acts_as_one_because_the_Event_is_theirs(
        EventStage stage,
        bool canConfigure
    )
    {
        var developer = Caller.Of(TestId.Of(33), isDeveloper: true);
        var scope = AccessScope.ForEvent(TENANT, TestId.Of(33), stage);

        Assert.Equal(canConfigure, AccessPolicy.Decide(Capability.ConfigureEvent, developer, scope).IsAllowed);
    }

    [Theory]
    [InlineData(EventStage.Unstarted)]
    [InlineData(EventStage.Live)]
    [InlineData(EventStage.Historic)]
    public void Nobody_is_the_Main_Operator_of_an_Event_that_has_none(EventStage stage)
    {
        var anAccount = Caller.Of(TestId.Of(34));
        var scope = AccessScope.ForEvent(TENANT, mainOperatorId: null, stage);

        Assert.False(AccessPolicy.Decide(Capability.ConfigureEvent, anAccount, scope).IsAllowed);
        Assert.False(AccessPolicy.Decide(Capability.HandOverMainOperator, anAccount, scope).IsAllowed);
        Assert.False(AccessPolicy.Decide(Capability.SendSnapshot, anAccount, scope).IsAllowed);
        Assert.False(AccessPolicy.IsMainOperator(anAccount, scope));
        Assert.False(AccessPolicy.IsMainOperator(Caller.Anonymous, scope));
    }

    [Fact]
    public void An_Event_is_not_created_in_a_Tenant_that_is_not_operational_by_anybody()
    {
        var tenantRoot = Caller.Of(TENANT_ROOT_ID, tenantRootIn: [TENANT]);
        var developer = Caller.Of(TestId.Of(20), isDeveloper: true);
        var notOperational = AccessScope.ForTenant(TENANT, isOperational: false);

        Assert.Equal(
            Refusal.TenantNotOperational,
            AccessPolicy.Decide(Capability.CreateEvent, tenantRoot, notOperational).Reason
        );
        Assert.Equal(
            Refusal.TenantNotOperational,
            AccessPolicy.Decide(Capability.CreateEvent, developer, notOperational).Reason
        );
    }

    [Theory]
    [InlineData(Who.Anonymous, Capability.SendSnapshot, EventStage.Live, Refusal.NotSignedIn)]
    [InlineData(Who.Anonymous, Capability.ConfigureEvent, EventStage.Unstarted, Refusal.NotSignedIn)]
    [InlineData(Who.SomeAccount, Capability.SendSnapshot, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.VeterinaryOfficial, Capability.SendSnapshot, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.SomeAccount, Capability.SeeTimeEvents, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.SomeAccount, Capability.SendSnapshot, EventStage.Historic, Refusal.NotAllowed)]
    [InlineData(Who.SomeAccount, Capability.SendSnapshot, EventStage.Unstarted, Refusal.NotAllowed)]
    [InlineData(Who.TenantRoot, Capability.ConfigureEvent, EventStage.Unstarted, Refusal.NotMainOperator)]
    [InlineData(Who.TenantRoot, Capability.ConfigureEvent, EventStage.Historic, Refusal.NotMainOperator)]
    [InlineData(Who.TenantRoot, Capability.HandOverMainOperator, EventStage.Live, Refusal.NotMainOperator)]
    [InlineData(Who.Developer, Capability.ConfigureEvent, EventStage.Live, Refusal.NotMainOperator)]
    [InlineData(Who.Developer, Capability.HandOverMainOperator, EventStage.Live, Refusal.NotMainOperator)]
    [InlineData(Who.MainOperator, Capability.AssignMainOperator, EventStage.Unstarted, Refusal.NotTenantRoot)]
    [InlineData(Who.MainOperator, Capability.AssignMainOperator, EventStage.Live, Refusal.NotTenantRoot)]
    [InlineData(Who.MainOperator, Capability.DeleteEvent, EventStage.Unstarted, Refusal.NotTenantRoot)]
    [InlineData(Who.TenantRoot, Capability.AssignMainOperator, EventStage.Live, Refusal.EventStarted)]
    [InlineData(Who.TenantRoot, Capability.AssignMainOperator, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.TenantRoot, Capability.DeleteEvent, EventStage.Live, Refusal.EventStarted)]
    [InlineData(Who.TenantRoot, Capability.DeleteEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.MainOperator, Capability.HandOverMainOperator, EventStage.Unstarted, Refusal.EventNotStarted)]
    [InlineData(Who.MainOperator, Capability.HandOverMainOperator, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.MainOperator, Capability.ResetEvent, EventStage.Unstarted, Refusal.EventNotStarted)]
    [InlineData(Who.MainOperator, Capability.ResetEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.MainOperator, Capability.ConfigureEvent, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.MainOperator, Capability.LinkAccounts, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.Steward, Capability.SendSnapshot, EventStage.Unstarted, Refusal.EventNotStarted)]
    [InlineData(Who.Steward, Capability.SendSnapshot, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.SomeAccount, Capability.EditSetup, EventStage.Unstarted, Refusal.NotMainOperator)]
    [InlineData(Who.TenantRoot, Capability.EditSetup, EventStage.Unstarted, Refusal.NotMainOperator)]
    [InlineData(Who.Developer, Capability.EditSetup, EventStage.Live, Refusal.NotMainOperator)]
    [InlineData(Who.MainOperator, Capability.EditSetup, EventStage.Live, Refusal.EventStarted)]
    [InlineData(Who.MainOperator, Capability.EditSetup, EventStage.Historic, Refusal.EventEnded)]
    [InlineData(Who.Anonymous, Capability.ReadSetup, EventStage.Unstarted, Refusal.NotSignedIn)]
    [InlineData(Who.SomeAccount, Capability.ReadSetup, EventStage.Unstarted, Refusal.NotAllowed)]
    [InlineData(Who.Steward, Capability.ReadSetup, EventStage.Live, Refusal.NotAllowed)]
    [InlineData(Who.TenantRootOfAnotherTenant, Capability.ReadSetup, EventStage.Historic, Refusal.NotAllowed)]
    public void A_refusal_says_what_is_missing_the_role_before_the_stage(
        Who who,
        Capability capability,
        EventStage stage,
        Refusal expected
    )
    {
        var person = People().Single(x => x.Who == who);

        var verdict = AccessPolicy.Decide(capability, person.Caller, AboutTheEvent(stage, person));

        Assert.False(verdict.IsAllowed);
        Assert.Equal(expected, verdict.Reason);
    }

    [Theory]
    [InlineData(Who.Anonymous, Capability.CreateEvent, Refusal.NotSignedIn)]
    [InlineData(Who.SomeAccount, Capability.CreateEvent, Refusal.NotTenantRoot)]
    [InlineData(Who.SomeAccount, Capability.EditTenantRules, Refusal.NotTenantRoot)]
    [InlineData(Who.MainOperator, Capability.EditTenantBranding, Refusal.NotTenantRoot)]
    [InlineData(Who.SomeAccount, Capability.EditRegistry, Refusal.NotAllowed)]
    [InlineData(Who.SomeAccount, Capability.SearchAccounts, Refusal.NotAllowed)]
    public void A_refusal_of_an_action_of_a_Tenant_says_what_is_missing(
        Who who,
        Capability capability,
        Refusal expected
    )
    {
        var person = People().Single(x => x.Who == who);

        var verdict = AccessPolicy.Decide(capability, person.Caller, AccessScope.ForTenant(TENANT, true));

        Assert.False(verdict.IsAllowed);
        Assert.Equal(expected, verdict.Reason);
    }

    [Theory]
    [InlineData(Who.TenantRoot, Capability.SeedTenantRoot, Refusal.NotDeveloper)]
    [InlineData(Who.TenantRoot, Capability.GrantDeveloper, Refusal.NotDeveloper)]
    [InlineData(Who.Anonymous, Capability.ReadRegistryAcrossTenants, Refusal.NotSignedIn)]
    [InlineData(Who.SomeAccount, Capability.SearchAccountsAcrossTenants, Refusal.NotAllowed)]
    [InlineData(Who.TenantRoot, Capability.EditCountries, Refusal.NotDeveloper)]
    [InlineData(Who.Anonymous, Capability.EditCountries, Refusal.NotSignedIn)]
    public void A_refusal_of_an_action_of_the_platform_says_what_is_missing(
        Who who,
        Capability capability,
        Refusal expected
    )
    {
        var person = People().Single(x => x.Who == who);

        var verdict = AccessPolicy.Decide(capability, person.Caller, AccessScope.Platform);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(expected, verdict.Reason);
    }

    [Fact]
    public void Public_views_are_allowed_to_everybody_and_say_nothing_when_they_are()
    {
        var verdict = AccessPolicy.Decide(Capability.ReadPublicViews, Caller.Anonymous, AccessScope.Platform);

        Assert.True(verdict.IsAllowed);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void An_action_is_asked_about_in_the_scope_it_belongs_to()
    {
        var caller = Caller.Of(TestId.Of(40));

        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.ConfigureEvent, caller, AccessScope.ForTenant(TENANT, true))
        );
        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.CreateEvent, caller, AccessScope.Platform)
        );
        Assert.Throws<ArgumentException>(
            () =>
                AccessPolicy.Decide(
                    Capability.SeedTenantRoot,
                    caller,
                    AccessScope.ForEvent(TENANT, MAIN_OPERATOR_ID, EventStage.Live)
                )
        );
    }

    [Fact]
    public void An_action_asked_about_in_the_wrong_scope_is_a_mistake_even_when_nobody_is_signed_in()
    {
        Assert.Throws<ArgumentException>(
            () => AccessPolicy.Decide(Capability.ConfigureEvent, Caller.Anonymous, AccessScope.Platform)
        );
    }

    [Fact]
    public void The_roles_that_may_send_a_Snapshot_are_the_four_the_domain_names()
    {
        OfficialRole[] expected =
        [
            OfficialRole.Steward,
            OfficialRole.ChiefSteward,
            OfficialRole.GroundJury,
            OfficialRole.GroundJuryPresident,
        ];

        Assert.Equal(expected, AccessPolicy.SnapshotOfficialRoles);
    }

    static AccessScope AboutTheEvent(EventStage stage, Person person)
    {
        return AccessScope.ForEvent(TENANT, MAIN_OPERATOR_ID, stage, person.Grants);
    }

    /// <summary>
    /// Everybody the matrix mentions, in relation to one Event of the Tenant whose Main Operator was appointed by a
    /// Tenant Root.
    /// </summary>
    static Person[] People()
    {
        return
        [
            new(Who.Anonymous, Caller.Anonymous),
            new(Who.SomeAccount, Caller.Of(TestId.Of(10))),
            new(Who.Operator, Caller.Of(TestId.Of(11)), CallerGrants.AsOperator()),
            new(Who.Steward, Caller.Of(TestId.Of(12)), CallerGrants.AsOfficial(OfficialRole.Steward)),
            new(Who.ChiefSteward, Caller.Of(TestId.Of(13)), CallerGrants.AsOfficial(OfficialRole.ChiefSteward)),
            new(Who.GroundJury, Caller.Of(TestId.Of(14)), CallerGrants.AsOfficial(OfficialRole.GroundJury)),
            new(
                Who.GroundJuryPresident,
                Caller.Of(TestId.Of(15)),
                CallerGrants.AsOfficial(OfficialRole.GroundJuryPresident)
            ),
            new(
                Who.VeterinaryOfficial,
                Caller.Of(TestId.Of(16)),
                CallerGrants.AsOfficial(OfficialRole.VeterinaryCommissionMember)
            ),
            new(Who.MainOperator, Caller.Of(MAIN_OPERATOR_ID, openMainOperatorIn: [TENANT])),
            new(Who.TenantRoot, Caller.Of(TENANT_ROOT_ID, tenantRootIn: [TENANT])),
            new(Who.TenantRootOfAnotherTenant, Caller.Of(TestId.Of(17), tenantRootIn: [OTHER_TENANT])),
            new(Who.MainOperatorOfAnotherEventOfTheTenant, Caller.Of(TestId.Of(18), openMainOperatorIn: [TENANT])),
            new(Who.MainOperatorOfAnotherTenantsEvent, Caller.Of(TestId.Of(19), openMainOperatorIn: [OTHER_TENANT])),
            new(Who.Developer, Caller.Of(TestId.Of(20), isDeveloper: true)),
        ];
    }

    static Capability[] EventCapabilities()
    {
        return
        [
            Capability.SeeTimeEvents,
            Capability.SendSnapshot,
            Capability.ConfigureEvent,
            Capability.EditSetup,
            Capability.ReadSetup,
            Capability.AssignMainOperator,
            Capability.HandOverMainOperator,
            Capability.LinkAccounts,
            Capability.EditEventBranding,
            Capability.ResetEvent,
            Capability.DeleteEvent,
        ];
    }

    static Who[] Everybody()
    {
        return [.. Enum.GetValues<Who>()];
    }

    static Who[] SignedIn()
    {
        return [.. Everybody().Where(x => x != Who.Anonymous)];
    }

    static Who[] Staff()
    {
        return
        [
            Who.Operator,
            Who.Steward,
            Who.ChiefSteward,
            Who.GroundJury,
            Who.GroundJuryPresident,
            Who.VeterinaryOfficial,
            Who.MainOperator,
        ];
    }

    static Who[] WhoCanSnapshot()
    {
        return [Who.Operator, Who.Steward, Who.ChiefSteward, Who.GroundJury, Who.GroundJuryPresident, Who.MainOperator];
    }

    static Who[] WhoHoldsAuthorityInTheTenant()
    {
        return [Who.TenantRoot, Who.MainOperator, Who.MainOperatorOfAnotherEventOfTheTenant, Who.Developer];
    }

    /// <summary>
    /// What ADR-0012 says about each action of an Event, at each stage of it. An action that is missing at a stage is
    /// allowed to nobody at that stage.
    /// </summary>
    static Dictionary<(Capability, EventStage), Who[]> EventRows()
    {
        return new()
        {
            // "See how times were recorded: Staff: Officials, Operators, the Main Operator". The Developer has every right.
            [(Capability.SeeTimeEvents, EventStage.Unstarted)] = [.. Staff(), Who.Developer],
            [(Capability.SeeTimeEvents, EventStage.Live)] = [.. Staff(), Who.Developer],
            [(Capability.SeeTimeEvents, EventStage.Historic)] = [.. Staff(), Who.Developer],
            // "Send a Snapshot: An Official in {Steward, ChiefSteward, GroundJury, GroundJuryPresident}, any Operator, the Main
            // Operator". Only a Live Event takes one, and the Developer's every right stops short of acting as its Main Operator.
            [(Capability.SendSnapshot, EventStage.Live)] = WhoCanSnapshot(),
            // "Configure an Event and use the Console: The Main Operator".
            [(Capability.ConfigureEvent, EventStage.Unstarted)] = [Who.MainOperator, Who.Developer],
            [(Capability.ConfigureEvent, EventStage.Live)] = [Who.MainOperator],
            // The Setup is what the Event is configured with before it starts: the Main Operator edits it until the Event
            // starts and then the Console works on the copies in the Core, so a started Event's Setup is changed by nobody.
            [(Capability.EditSetup, EventStage.Unstarted)] = [Who.MainOperator, Who.Developer],
            // Reading a Setup is for the people who run the Event and the Tenant that holds it, at every stage.
            [(Capability.ReadSetup, EventStage.Unstarted)] = [Who.MainOperator, Who.TenantRoot, Who.Developer],
            [(Capability.ReadSetup, EventStage.Live)] = [Who.MainOperator, Who.TenantRoot, Who.Developer],
            [(Capability.ReadSetup, EventStage.Historic)] = [Who.MainOperator, Who.TenantRoot, Who.Developer],
            // "Assign the Main Operator: The Tenant Root while the Event is not [yet] Live; once Live, only the Main Operator
            // hands it over".
            [(Capability.AssignMainOperator, EventStage.Unstarted)] = [Who.TenantRoot, Who.Developer],
            [(Capability.HandOverMainOperator, EventStage.Live)] = [Who.MainOperator],
            // "Link accounts to Officials and Operators: The Main Operator".
            [(Capability.LinkAccounts, EventStage.Unstarted)] = [Who.MainOperator, Who.Developer],
            [(Capability.LinkAccounts, EventStage.Live)] = [Who.MainOperator],
            // "an Event's branding override: the Main Operator".
            [(Capability.EditEventBranding, EventStage.Unstarted)] = [Who.MainOperator, Who.Developer],
            [(Capability.EditEventBranding, EventStage.Live)] = [Who.MainOperator],
            // "The Main Operator may reset while Live; a Tenant Root may delete before it starts; neither once Historic".
            [(Capability.ResetEvent, EventStage.Live)] = [Who.MainOperator],
            [(Capability.DeleteEvent, EventStage.Unstarted)] = [Who.TenantRoot, Who.Developer],
        };
    }

    /// <summary>The actions of one Tenant, whatever the stage of its Events.</summary>
    static Dictionary<Capability, Who[]> TenantRows()
    {
        return new()
        {
            // "Create an Event: A Tenant Root".
            [Capability.CreateEvent] = [Who.TenantRoot, Who.Developer],
            // "Edit the Tenant's registry: A Tenant Root, and the Main Operator of any Event of that Tenant that is not yet Historic".
            [Capability.EditRegistry] = WhoHoldsAuthorityInTheTenant(),
            // "Tenant rules and Tenant branding: A Tenant Root".
            [Capability.EditTenantRules] = [Who.TenantRoot, Who.Developer],
            [Capability.EditTenantBranding] = [Who.TenantRoot, Who.Developer],
            // "Search accounts by name: A Tenant Root, the Main Operator".
            [Capability.SearchAccounts] = WhoHoldsAuthorityInTheTenant(),
        };
    }

    /// <summary>The actions that belong to no Tenant and no Event.</summary>
    static Dictionary<Capability, Who[]> PlatformRows()
    {
        return new()
        {
            // "Read public Event views: Anyone, signed in or not".
            [Capability.ReadPublicViews] = Everybody(),
            // "Read an Athlete's or Horse's history, search the registry across Tenants: Any signed-in account".
            [Capability.ReadRegistryAcrossTenants] = SignedIn(),
            // The account search "reaches across Tenants only when the caller asks for it explicitly": for the people who may
            // search at all, in any Tenant.
            [Capability.SearchAccountsAcrossTenants] =
            [
                Who.TenantRoot,
                Who.TenantRootOfAnotherTenant,
                Who.MainOperator,
                Who.MainOperatorOfAnotherEventOfTheTenant,
                Who.MainOperatorOfAnotherTenantsEvent,
                Who.Developer,
            ],
            // "Seed a Tenant Root, grant Developer: The Developer, by command".
            [Capability.SeedTenantRoot] = [Who.Developer],
            [Capability.GrantDeveloper] = [Who.Developer],
            // The countries are the platform's reference data, which the owner of the platform keeps.
            [Capability.EditCountries] = [Who.Developer],
        };
    }

    sealed class Person
    {
        public Person(Who who, Caller caller, CallerGrants? grants = null)
        {
            Who = who;
            Caller = caller;
            Grants = grants ?? CallerGrants.None;
        }

        public Who Who { get; }
        public Caller Caller { get; }
        public CallerGrants Grants { get; }
    }

    public enum Who
    {
        Anonymous,
        SomeAccount,
        Operator,
        Steward,
        ChiefSteward,
        GroundJury,
        GroundJuryPresident,
        VeterinaryOfficial,
        MainOperator,
        TenantRoot,
        TenantRootOfAnotherTenant,
        MainOperatorOfAnotherEventOfTheTenant,
        MainOperatorOfAnotherTenantsEvent,
        Developer,
    }
}
