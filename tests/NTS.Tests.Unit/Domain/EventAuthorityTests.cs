using NoTiming.Api.Features.Events.Factories;
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
/// An Event belongs to one Tenant (ADR-0012). The Tenant is fixed when the Event is created and travels with it when it
/// starts, so the Event that is Live is the one Root created, in the Tenant it was created in. Who runs it is not part of
/// the Event: Root runs every Event of the Tenant, and the Console Operators are grants (#671). An Event from before
/// Tenants has the constant Tenant of those days.
/// </summary>
public sealed class EventAuthorityTests
{
    static readonly DateTimeOffset START = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly Country BULGARIA = new(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");

    [Fact]
    public void An_Event_that_starts_keeps_the_Tenant_of_its_Setup()
    {
        var setup = CreateSetupEvent("country-bg");

        var started = EventInformationFactory.Create(setup, RegionalRules.None);

        Assert.Equal("country-bg", started.TenantId);
    }

    [Fact]
    public void A_Setup_from_before_Tenants_has_the_constant_Tenant_and_starts_as_such()
    {
        var setup = CreateSetupEvent(null);

        var started = EventInformationFactory.Create(setup);

        Assert.Equal(Tenant.LEGACY_ID, setup.TenantId);
        Assert.Equal(Tenant.LEGACY_ID, started.TenantId);
    }

    [Fact]
    public void The_Tenant_of_a_Setup_is_kept_by_the_model_it_is_stored_as()
    {
        var setup = CreateSetupEvent("country-bg");

        var loaded = ConfigureEventModel.From(setup).MapToEntity();

        Assert.Equal("country-bg", loaded.TenantId);
    }

    [Fact]
    public void The_Tenant_of_a_started_Event_is_kept_by_the_model_it_is_stored_as()
    {
        var started = EventInformationFactory.Create(CreateSetupEvent("country-bg"));

        var loaded = EventInformationModel.From(started).MapToEntity();

        Assert.Equal("country-bg", loaded.TenantId);
    }

    [Fact]
    public void A_stored_Event_that_has_no_Tenant_loads_with_the_constant_Tenant()
    {
        var started = EventInformationFactory.Create(CreateSetupEvent("country-bg"));
        var stored = EventInformationModel.From(started);
        stored.TenantId = Tenant.LEGACY_ID;

        var loaded = stored.MapToEntity();

        Assert.Equal(Tenant.LEGACY_ID, loaded.TenantId);
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
    public void A_grant_for_an_Operator_or_a_Console_Operator_has_no_role_and_one_for_an_Official_has_the_role_of_the_Official()
    {
        var operatorGrant = EventGrant.ForOperator(TestId.Of(1), TestId.Of(300), "country-bg", " Ana@Example.test ");
        var consoleOperatorGrant = EventGrant.ForConsoleOperator(
            TestId.Of(3),
            TestId.Of(300),
            "country-bg",
            " Vera@Example.test "
        );
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
        Assert.Equal(GrantKind.ConsoleOperator, consoleOperatorGrant.Kind);
        Assert.Null(consoleOperatorGrant.OfficialRole);
        Assert.Equal("vera@example.test", consoleOperatorGrant.Email);
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

    static ConfigureEvent CreateSetupEvent(string? tenantId)
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
            tenantId: tenantId
        );
    }
}
