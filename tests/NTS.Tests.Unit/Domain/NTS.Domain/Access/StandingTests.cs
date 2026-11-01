using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Access;

/// <summary>
/// Where an account stands at a place (ADR-0012): the Level the facts about it make at an Event, at a Tenant and at the
/// platform. What each case expects is written out from the ADR and the glossary, and the policy is not asked.
/// </summary>
public sealed class StandingTests
{
    const string TENANT = "country-bg";
    static readonly Guid AN_EVENT = TestId.Of(300);
    static readonly Guid AN_ACCOUNT = TestId.Of(9);

    [Fact]
    public void A_signed_in_account_that_holds_nothing_stands_as_an_Account_who_may_not_record()
    {
        Assert.Equal(AccessLevel.Account, Standing.Account.Level);
        Assert.False(Standing.Account.MayRecord);
        Assert.Equal(AccessLevel.Anonymous, Standing.Anonymous.Level);
    }

    [Fact]
    public void An_account_that_holds_nothing_on_an_Event_is_an_Account_who_may_not_record()
    {
        var standing = Standing.AtEvent(Roles.None, TENANT, []);

        Assert.Equal(AccessLevel.Account, standing.Level);
        Assert.False(standing.MayRecord);
    }

    [Fact]
    public void An_Operator_is_Staff_who_may_record()
    {
        var standing = Standing.AtEvent(Roles.None, TENANT, [AnOperator()]);

        Assert.Equal(AccessLevel.Staff, standing.Level);
        Assert.True(standing.MayRecord);
    }

    [Theory]
    [InlineData(OfficialRole.Steward)]
    [InlineData(OfficialRole.ChiefSteward)]
    [InlineData(OfficialRole.GroundJury)]
    [InlineData(OfficialRole.GroundJuryPresident)]
    public void An_Official_of_the_four_roles_that_record_is_Staff_who_may_record(OfficialRole role)
    {
        var standing = Standing.AtEvent(Roles.None, TENANT, [AnOfficial(role)]);

        Assert.Equal(AccessLevel.Staff, standing.Level);
        Assert.True(standing.MayRecord);
    }

    [Fact]
    public void An_Official_of_any_other_role_is_Staff_who_sees_how_times_were_recorded_and_records_none()
    {
        OfficialRole[] recording =
        [
            OfficialRole.Steward,
            OfficialRole.ChiefSteward,
            OfficialRole.GroundJury,
            OfficialRole.GroundJuryPresident,
        ];

        foreach (var role in Enum.GetValues<OfficialRole>().Except(recording))
        {
            var standing = Standing.AtEvent(Roles.None, TENANT, [AnOfficial(role)]);

            Assert.Equal(AccessLevel.Staff, standing.Level);
            Assert.False(standing.MayRecord);
        }
    }

    [Fact]
    public void The_grants_of_an_account_add_up_so_that_one_that_records_makes_Staff_who_may()
    {
        var standing = Standing.AtEvent(
            Roles.None,
            TENANT,
            [AnOfficial(OfficialRole.VeterinaryCommissionMember), AnOfficial(OfficialRole.Steward)]
        );

        Assert.Equal(AccessLevel.Staff, standing.Level);
        Assert.True(standing.MayRecord);
    }

    [Fact]
    public void A_pending_grant_gives_nothing_until_it_has_an_account()
    {
        var pending = EventGrant.ForOperator(TestId.Of(3), AN_EVENT, TENANT, "a@x.test");

        var standing = Standing.AtEvent(Roles.None, TENANT, [pending]);

        Assert.Equal(AccessLevel.Account, standing.Level);
        Assert.False(standing.MayRecord);
    }

    [Fact]
    public void A_Console_Operator_grant_makes_a_Console_Operator_who_records()
    {
        var standing = Standing.AtEvent(Roles.None, TENANT, [AConsoleOperator()]);

        Assert.Equal(AccessLevel.ConsoleOperator, standing.Level);
        Assert.True(standing.MayRecord);
    }

    [Fact]
    public void A_Console_Operator_grant_outweighs_the_grants_of_Staff()
    {
        var standing = Standing.AtEvent(
            Roles.None,
            TENANT,
            [AnOfficial(OfficialRole.TechnicalDelegate), AConsoleOperator()]
        );

        Assert.Equal(AccessLevel.ConsoleOperator, standing.Level);
    }

    [Fact]
    public void A_pending_Console_Operator_grant_gives_nothing_until_it_has_an_account()
    {
        var pending = EventGrant.ForConsoleOperator(TestId.Of(4), AN_EVENT, TENANT, "a@x.test");

        var standing = Standing.AtEvent(Roles.None, TENANT, [pending]);

        Assert.Equal(AccessLevel.Account, standing.Level);
    }

    [Fact]
    public void The_Developer_is_Root_at_an_Event_of_any_Tenant()
    {
        var standing = Standing.AtEvent(Roles.Of(isDeveloper: true), "country-tr", []);

        Assert.Equal(AccessLevel.Root, standing.Level);
        Assert.True(standing.MayRecord);
    }

    [Fact]
    public void A_Tenant_Root_is_Root_at_the_Events_of_its_Tenant()
    {
        var standing = Standing.AtEvent(Roles.Of(tenantRootIn: [TENANT]), TENANT, []);

        Assert.Equal(AccessLevel.Root, standing.Level);
    }

    [Fact]
    public void A_Tenant_Root_has_no_authority_at_an_Event_of_another_Tenant_and_stands_there_by_its_grants()
    {
        var root = Roles.Of(tenantRootIn: ["country-tr"]);

        var without = Standing.AtEvent(root, TENANT, []);
        var withAGrant = Standing.AtEvent(root, TENANT, [AnOperator()]);

        Assert.Equal(AccessLevel.Account, without.Level);
        Assert.Equal(AccessLevel.Staff, withAGrant.Level);
    }

    [Fact]
    public void A_Tenant_is_told_by_its_key_as_it_is_written_and_not_by_another_spelling_of_it()
    {
        var root = Roles.Of(tenantRootIn: [TENANT]);

        Assert.True(root.IsRootAt(TENANT));
        Assert.False(root.IsRootAt(TENANT.ToUpperInvariant()));
    }

    [Fact]
    public void Root_outweighs_the_grants_it_holds()
    {
        var standing = Standing.AtEvent(Roles.Of(tenantRootIn: [TENANT]), TENANT, [AnOperator()]);

        Assert.Equal(AccessLevel.Root, standing.Level);
    }

    [Fact]
    public void An_account_that_is_a_Console_Operator_of_no_open_Event_of_a_Tenant_is_an_Account_there()
    {
        var standing = Standing.AtTenant(Roles.None, TENANT, isConsoleOperatorOfAnOpenEvent: false);

        Assert.Equal(AccessLevel.Account, standing.Level);
    }

    [Fact]
    public void A_Console_Operator_of_an_Event_of_the_Tenant_that_has_not_ended_is_a_Console_Operator_at_the_Tenant()
    {
        var standing = Standing.AtTenant(Roles.None, TENANT, isConsoleOperatorOfAnOpenEvent: true);

        Assert.Equal(AccessLevel.ConsoleOperator, standing.Level);
    }

    [Fact]
    public void The_Developer_and_a_Tenant_Root_of_the_Tenant_are_Root_at_it_whatever_Events_they_run()
    {
        var developer = Standing.AtTenant(Roles.Of(isDeveloper: true), TENANT, isConsoleOperatorOfAnOpenEvent: false);
        var tenantRoot = Standing.AtTenant(
            Roles.Of(tenantRootIn: [TENANT]),
            TENANT,
            isConsoleOperatorOfAnOpenEvent: true
        );

        Assert.Equal(AccessLevel.Root, developer.Level);
        Assert.Equal(AccessLevel.Root, tenantRoot.Level);
    }

    [Fact]
    public void A_Tenant_Root_of_another_Tenant_is_an_Account_at_this_one()
    {
        var standing = Standing.AtTenant(
            Roles.Of(tenantRootIn: ["country-tr"]),
            TENANT,
            isConsoleOperatorOfAnOpenEvent: false
        );

        Assert.Equal(AccessLevel.Account, standing.Level);
    }

    [Fact]
    public void The_Developer_alone_is_Root_at_the_platform()
    {
        var developer = Standing.AtPlatform(Roles.Of(isDeveloper: true), isConsoleOperatorOfAnOpenEvent: false);
        var tenantRoot = Standing.AtPlatform(Roles.Of(tenantRootIn: [TENANT]), isConsoleOperatorOfAnOpenEvent: false);

        Assert.Equal(AccessLevel.Root, developer.Level);
        Assert.NotEqual(AccessLevel.Root, tenantRoot.Level);
    }

    [Fact]
    public void A_Tenant_Root_of_any_Tenant_sets_Events_up_so_it_stands_as_a_Console_Operator_at_the_platform()
    {
        var standing = Standing.AtPlatform(
            Roles.Of(tenantRootIn: ["country-tr"]),
            isConsoleOperatorOfAnOpenEvent: false
        );

        Assert.Equal(AccessLevel.ConsoleOperator, standing.Level);
    }

    [Fact]
    public void A_Console_Operator_of_any_Event_that_has_not_ended_stands_as_a_Console_Operator_at_the_platform()
    {
        var standing = Standing.AtPlatform(Roles.None, isConsoleOperatorOfAnOpenEvent: true);

        Assert.Equal(AccessLevel.ConsoleOperator, standing.Level);
    }

    [Fact]
    public void An_account_that_sets_nothing_up_is_an_Account_at_the_platform()
    {
        var standing = Standing.AtPlatform(Roles.None, isConsoleOperatorOfAnOpenEvent: false);

        Assert.Equal(AccessLevel.Account, standing.Level);
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

        Assert.Equal(expected, Standing.SnapshotOfficialRoles);
    }

    static EventGrant AConsoleOperator()
    {
        return EventGrant.ForConsoleOperator(TestId.Of(5), AN_EVENT, TENANT, "a@x.test", AN_ACCOUNT);
    }

    static EventGrant AnOperator()
    {
        return EventGrant.ForOperator(TestId.Of(1), AN_EVENT, TENANT, "a@x.test", AN_ACCOUNT);
    }

    static EventGrant AnOfficial(OfficialRole role)
    {
        return EventGrant.ForOfficial(TestId.Of(2), AN_EVENT, TENANT, role, "a@x.test", AN_ACCOUNT);
    }
}
