using NTS.Application.Factories;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using ConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// An Event belongs to one Tenant and has one Main Operator (ADR-0012). Both are fixed when the Event is created and
/// travel with it when it starts, so the Event that is Live is the one its Tenant Root created, in the hands of the
/// person the Tenant Root named. An Event from before Tenants has the constant Tenant of those days and nobody to run it.
/// </summary>
public sealed class EventAuthorityTests
{
    static readonly DateTimeOffset START = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly Country BULGARIA = new(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");

    [Fact]
    public void An_Event_that_starts_keeps_the_Tenant_and_the_Main_Operator_of_its_Setup()
    {
        var setup = CreateSetupEvent("country-bg", TestId.Of(77));

        var started = EventInformationFactory.Create(setup, RegionalRules.None);

        Assert.Equal("country-bg", started.TenantId);
        Assert.Equal(TestId.Of(77), started.MainOperatorId);
    }

    [Fact]
    public void A_Setup_from_before_Tenants_has_the_constant_Tenant_and_no_Main_Operator_and_starts_as_such()
    {
        var setup = CreateSetupEvent(null, null);

        var started = EventInformationFactory.Create(setup);

        Assert.Equal(Tenant.LEGACY_ID, setup.TenantId);
        Assert.Null(setup.MainOperatorId);
        Assert.Equal(Tenant.LEGACY_ID, started.TenantId);
        Assert.Null(started.MainOperatorId);
    }

    [Fact]
    public void The_Tenant_and_the_Main_Operator_of_a_Setup_are_kept_by_the_model_it_is_stored_as()
    {
        var setup = CreateSetupEvent("country-bg", TestId.Of(77));

        var loaded = ConfigureEventModel.From(setup).MapToEntity();

        Assert.Equal("country-bg", loaded.TenantId);
        Assert.Equal(TestId.Of(77), loaded.MainOperatorId);
    }

    [Fact]
    public void The_Tenant_and_the_Main_Operator_of_a_started_Event_are_kept_by_the_model_it_is_stored_as()
    {
        var started = EventInformationFactory.Create(CreateSetupEvent("country-bg", TestId.Of(77)));

        var loaded = EventInformationModel.From(started).MapToEntity();

        Assert.Equal("country-bg", loaded.TenantId);
        Assert.Equal(TestId.Of(77), loaded.MainOperatorId);
    }

    [Fact]
    public void A_stored_Event_that_has_no_Tenant_or_Main_Operator_loads_with_the_constant_Tenant_and_none()
    {
        var started = EventInformationFactory.Create(CreateSetupEvent("country-bg", TestId.Of(77)));
        var stored = EventInformationModel.From(started);
        stored.TenantId = Tenant.LEGACY_ID;
        stored.MainOperatorId = null;

        var loaded = stored.MapToEntity();

        Assert.Equal(Tenant.LEGACY_ID, loaded.TenantId);
        Assert.Null(loaded.MainOperatorId);
    }

    [Fact]
    public void A_Tenant_Root_holds_the_role_in_a_Membership_of_one_Tenant_and_gaining_it_twice_changes_nothing()
    {
        var membership = new Membership("country-bg");

        var root = membership.WithRole(Membership.TENANT_ROOT);
        var again = root.WithRole(Membership.TENANT_ROOT);

        Assert.False(membership.IsTenantRoot);
        Assert.True(root.IsTenantRoot);
        Assert.Equal(["tenant-root"], again.Roles);
        Assert.Equal("country-bg", again.TenantId);
    }

    [Fact]
    public void A_role_is_added_to_the_roles_a_Membership_holds_and_only_the_Tenant_Root_role_makes_a_Tenant_Root()
    {
        var withAnotherRole = new Membership("country-bg", ["scorer"]);

        var root = withAnotherRole.WithRole(Membership.TENANT_ROOT);

        Assert.False(withAnotherRole.IsTenantRoot);
        Assert.Equal(["scorer", "tenant-root"], root.Roles);
        Assert.True(root.IsTenantRoot);
    }

    [Fact]
    public void A_grant_for_an_Operator_has_no_role_and_one_for_an_Official_has_the_role_of_the_Official()
    {
        var operatorGrant = EventGrant.ForOperator(TestId.Of(1), TestId.Of(300), "country-bg", " Ana@Example.test ");
        var officialGrant = EventGrant.ForOfficial(
            TestId.Of(2),
            TestId.Of(300),
            "country-bg",
            OfficialRole.Steward,
            "boris@example.test"
        );

        Assert.Equal(GrantKind.Operator, operatorGrant.Kind);
        Assert.Null(operatorGrant.OfficialRole);
        Assert.Equal("ana@example.test", operatorGrant.Email);
        Assert.Equal(GrantKind.Official, officialGrant.Kind);
        Assert.Equal(OfficialRole.Steward, officialGrant.OfficialRole);
    }

    [Fact]
    public void A_grant_to_an_email_with_no_account_is_pending_and_attaches_to_the_account_that_registers_it()
    {
        var pending = EventGrant.ForOperator(TestId.Of(1), TestId.Of(300), "country-bg", "ana@example.test");

        var attached = pending.AttachedTo(TestId.Of(55));

        Assert.True(pending.IsPending);
        Assert.Null(pending.AccountId);
        Assert.False(attached.IsPending);
        Assert.Equal(TestId.Of(55), attached.AccountId);
        Assert.Equal(pending.Id, attached.Id);
        Assert.Equal(pending.Email, attached.Email);
    }

    [Fact]
    public void A_grant_that_has_an_account_is_not_moved_to_another()
    {
        var attached = EventGrant.ForOperator(
            TestId.Of(1),
            TestId.Of(300),
            "country-bg",
            "ana@example.test",
            TestId.Of(55)
        );

        Assert.Throws<InvalidOperationException>(() => attached.AttachedTo(TestId.Of(56)));
        Assert.Equal(attached, attached.AttachedTo(TestId.Of(55)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_grant_names_an_email(string email)
    {
        Assert.Throws<ArgumentException>(
            () => EventGrant.ForOperator(TestId.Of(1), TestId.Of(300), "country-bg", email)
        );
    }

    [Fact]
    public void What_an_account_has_been_granted_on_an_Event_is_every_Operator_grant_and_the_role_of_every_Official_one()
    {
        var grants = new[]
        {
            EventGrant.ForOfficial(
                TestId.Of(1),
                TestId.Of(300),
                "country-bg",
                OfficialRole.Steward,
                "a@x.test",
                TestId.Of(9)
            ),
            EventGrant.ForOfficial(
                TestId.Of(2),
                TestId.Of(300),
                "country-bg",
                OfficialRole.GroundJury,
                "a@x.test",
                TestId.Of(9)
            ),
            EventGrant.ForOperator(TestId.Of(3), TestId.Of(300), "country-bg", "a@x.test", TestId.Of(9)),
        };

        var held = CallerGrants.Of(grants);

        Assert.True(held.IsOperator);
        Assert.Equal([OfficialRole.Steward, OfficialRole.GroundJury], held.OfficialRoles.Order());
        Assert.True(held.MaySendSnapshot);
        Assert.False(CallerGrants.Of([]).IsStaff);
    }

    [Fact]
    public void A_pending_grant_gives_nothing_until_it_has_an_account()
    {
        var pending = EventGrant.ForOperator(TestId.Of(3), TestId.Of(300), "country-bg", "a@x.test");

        Assert.False(CallerGrants.Of([pending]).IsStaff);
    }

    static ConfigureEvent CreateSetupEvent(string? tenantId, Guid? mainOperatorId)
    {
        var loop = new SetupLoop(40, id: TestId.Of(4));
        var competition = new SetupCompetition(
            name: "Regional",
            ruleset: CompetitionRuleset.Regional,
            start: START,
            compulsoryThresholdSpan: null,
            minSpeedRestriction: null,
            maxSpeedRestriction: null,
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [new SetupPhase(loop, recovery: 40, rest: null, id: TestId.Of(5))],
            participations: [],
            id: TestId.Of(7)
        );

        return new ConfigureEvent(
            "Event",
            "Sofia",
            BULGARIA,
            null,
            [competition],
            [],
            [loop],
            [],
            id: TestId.Of(300),
            tenantId: tenantId,
            mainOperatorId: mainOperatorId
        );
    }
}
