using System.Security.Claims;
using Microsoft.Extensions.Localization;
using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using NTS.Application.Contracts.Arrivelists;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Presentlists;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Arrivelists;
using NTS.Domain.Core.Objects.Presentlists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Localization;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using NTS.Witness.Contracts.API;
using NTS.Witness.Contracts.Features.Access;
using NTS.Witness.Contracts.Features.Performance;
using NTS.Witness.Contracts.Features.Profile;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupOfficial = NTS.Domain.Setup.Aggregates.ConfigureEvents.Official;
using SetupOperator = NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;
using SetupUser = NTS.Domain.Setup.Aggregates.User;
using WitnessSnapshot = NTS.Domain.Core.Objects.Snapshots.Snapshot;
using WitnessSnapshotService = NTS.Witness.Contracts.Features.Snapshots.ISnapshotService;

namespace NTS.Tests.Integration;

public sealed partial class IntegrationHarnessCheckTest : IClassFixture<NtsIntegrationFixture>
{
    static readonly IntegrationUser OFFICIAL_USER = new(
        "official.witness@integration.test",
        "official-witness-user",
        "Official Witness"
    );
    static readonly IntegrationUser REGISTERED_USER = new(
        "registered.witness@integration.test",
        "registered-witness-user",
        "Registered Witness"
    );

    readonly NtsIntegrationFixture _fixture;

    public IntegrationHarnessCheckTest(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Witness_snapshot_selections_restore_from_user_session_until_published()
    {
        var eventId = 1901;
        var participationNumber = 61;
        var timestamp = DateTimeOffset.UtcNow.Date.AddHours(11).AddMinutes(17);
        var expectedTimestamp = new Timestamp(timestamp).ToString();
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId);
        var participation = IntegrationPayloadFactory.ActiveParticipation(
            eventId,
            participationNumber,
            id: 5701,
            startTime: timestamp.AddHours(-2)
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var officialUser = await api.RegisterUser(OFFICIAL_USER);
        await api.Create(eventInformation);
        await api.Create(participation);
        await api.Create(IntegrationPayloadFactory.Official(eventId, officialUser.Id, id: 6701));

        await using var witness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            OFFICIAL_USER,
            "SnapshotSessionWitness"
        );

        await witness.Start();
        await witness.Connect(eventInformation);

        var snapshots = witness.GetRequiredService<WitnessSnapshotService>();
        await snapshots.Load();
        snapshots.SelectForSnapshot(snapshots.Participations.Single(x => x.Combination.Number == participationNumber));

        await WaitForUserSession(
            api,
            OFFICIAL_USER.UserIdentifier,
            eventId,
            state =>
                state.SnapshotSelections.Length == 1
                && state.SnapshotSelections[0].Number == participationNumber
                && state.SnapshotSelections[0].Timestamp == null,
            "persist the selected snapshot without a timestamp"
        );

        var selectedSnapshot = snapshots.Snapshots.Single(x => x.Number == participationNumber);
        snapshots.UpdateTimestamp(selectedSnapshot, new Timestamp(timestamp));

        await WaitForUserSession(
            api,
            OFFICIAL_USER.UserIdentifier,
            eventId,
            state =>
                state.SnapshotSelections.Length == 1
                && state.SnapshotSelections[0].Number == participationNumber
                && state.SnapshotSelections[0].Timestamp == expectedTimestamp,
            "persist the captured snapshot timestamp"
        );

        await witness.Disconnect();

        await using var restoredWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            OFFICIAL_USER,
            "SnapshotSessionRestoredWitness"
        );

        await restoredWitness.Start();
        await restoredWitness.Connect(eventInformation);

        var restoredSnapshots = restoredWitness.GetRequiredService<WitnessSnapshotService>();
        await restoredSnapshots.Load();
        var restoredSnapshot = restoredSnapshots.Snapshots.Single(x => x.Number == participationNumber);

        Assert.Equal(expectedTimestamp, restoredSnapshot.Timestamp?.ToString());
        Assert.DoesNotContain(restoredSnapshots.Participations, x => x.Combination.Number == participationNumber);
        Assert.True(await restoredSnapshots.Publish(SnapshotType.Arrive));

        var publishedSession = await WaitForUserSession(
            api,
            OFFICIAL_USER.UserIdentifier,
            eventId,
            state =>
                state.SnapshotSelections.Length == 0
                && state.SnapshotHistory.Any(group =>
                    group.Type == SnapshotType.Arrive && group.Entries.Any(entry => entry.Number == participationNumber)
                ),
            "clear sent selections and append the snapshot history"
        );

        Assert.Empty(publishedSession.State!.SnapshotSelections);
        Assert.Contains(
            publishedSession.State.SnapshotHistory,
            group =>
                group.Type == SnapshotType.Arrive && group.Entries.Any(entry => entry.Number == participationNumber)
        );
    }

    [Fact]
    public async Task Operators_are_projected_and_gate_witness_write_access()
    {
        var eventId = 1801;
        var operatorIdentity = new IntegrationUser(
            "operator.witness@integration.test",
            "operator-witness-user",
            "Operator Witness"
        );
        var eligibleOfficialIdentity = new IntegrationUser(
            "eligible.official.witness@integration.test",
            "eligible-official-witness-user",
            "Eligible Official Witness"
        );
        var ineligibleOfficialIdentity = new IntegrationUser(
            "ineligible.official.witness@integration.test",
            "ineligible-official-witness-user",
            "Ineligible Official Witness"
        );
        var registeredIdentity = new IntegrationUser(
            "operator-registered.witness@integration.test",
            "operator-registered-witness-user",
            "Operator Registered Witness"
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var operatorUser = ToSetupUser(await api.RegisterUser(operatorIdentity));
        var eligibleOfficialUser = ToSetupUser(await api.RegisterUser(eligibleOfficialIdentity));
        var ineligibleOfficialUser = ToSetupUser(await api.RegisterUser(ineligibleOfficialIdentity));
        await api.RegisterUser(registeredIdentity);

        var setupEvent = CreateOperatorSetupEvent(eventId, operatorUser, eligibleOfficialUser, ineligibleOfficialUser);
        await api.CreateSetupConfigureEvent(setupEvent);

        var persistedSetup = await api.ReadSetupConfigureEvent(eventId);
        Assert.Single(persistedSetup.Operators);

        var eventInformation = await api.StartEventInformation(eventId);
        var activeOfficials = await api.ReadOfficials(eventInformation.Id);
        var activeOperators = await api.ReadOperators(eventInformation.Id);
        var activeRankings = await api.ReadRankings(eventInformation.Id);

        Assert.Equal(2, activeOfficials.Count);
        Assert.DoesNotContain(activeOfficials, x => x.UserId == operatorUser.Id);
        Assert.Single(activeOperators);
        Assert.Equal(operatorUser.Id, activeOperators[0].UserId);
        Assert.Equal(OfficialRole.Steward, activeOperators[0].Role);
        Assert.Single(activeRankings);

        await using var operatorWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            operatorIdentity,
            "IntegrationOperatorWitness"
        );
        await using var eligibleOfficialWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            eligibleOfficialIdentity,
            "IntegrationEligibleOfficialWitness"
        );
        await using var ineligibleOfficialWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            ineligibleOfficialIdentity,
            "IntegrationIneligibleOfficialWitness"
        );
        await using var registeredWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            registeredIdentity,
            "IntegrationOperatorRegisteredWitness"
        );

        await operatorWitness.Start();
        await eligibleOfficialWitness.Start();
        await ineligibleOfficialWitness.Start();
        await registeredWitness.Start();

        await operatorWitness.Connect(eventInformation);
        await eligibleOfficialWitness.Connect(eventInformation);
        await ineligibleOfficialWitness.Connect(eventInformation);
        await registeredWitness.Connect(eventInformation);

        Assert.Equal(WitnessAccessLevel.Official, operatorWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Official, eligibleOfficialWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Registered, ineligibleOfficialWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Registered, registeredWitness.AccessLevel);

        await operatorWitness.Publish(CreateSnapshotGroup());
        await eligibleOfficialWitness.Publish(CreateSnapshotGroup());
        var denied = await Assert.ThrowsAnyAsync<Exception>(
            () => ineligibleOfficialWitness.Publish(CreateSnapshotGroup())
        );
        Assert.Contains("Only authorized event staff", denied.Message);
    }

    [Fact]
    public async Task Witness_registration_resolution_creates_missing_nexus_user()
    {
        var registeringUser = new IntegrationUser(
            "registering.witness@integration.test",
            "registering-witness-user",
            "Rosa Maria Register",
            "Rosa",
            "Maria",
            "Register",
            "Bulgaria",
            "Konarche",
            "10101010",
            "Rosa Display"
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);
        await using var witness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            registeringUser,
            "IntegrationRegisteringWitness"
        );
        var resolver = witness.GetRequiredService<NUserResolver>();
        var principal = CreatePrincipal(registeringUser);
        var profile = new NUserRegistrationProfile(
            registeringUser.Name,
            registeringUser.GivenName,
            registeringUser.MiddleName,
            registeringUser.Surname,
            registeringUser.Club,
            registeringUser.FeiId,
            registeringUser.DisplayName
        );

        Assert.Null(await api.ReadUser(registeringUser.Email));

        var result = await resolver.ResolvePrincipal(principal, profile);

        Assert.True(result.IsSuccess, result.Error);
        var created = await api.ReadUser(registeringUser.Email);
        Assert.NotNull(created);
        Assert.Equal(registeringUser.Email, created!.Email);
        Assert.Equal(registeringUser.Name, created.Name);
        Assert.Equal(registeringUser.DisplayName, created.DisplayName);
        Assert.Equal(registeringUser.GivenName, created.GivenName);
        Assert.Equal(registeringUser.MiddleName, created.MiddleName);
        Assert.Equal(registeringUser.Surname, created.Surname);
        Assert.Equal(registeringUser.CountryRegion, created.CountryRegion);
        Assert.Equal(registeringUser.Club, created.Club);
        Assert.Equal(registeringUser.FeiId, created.FeiId);
    }

    [Fact]
    public async Task Witness_profile_update_completes_existing_email_only_user()
    {
        var profileUser = new IntegrationUser(
            "profile-completion.witness@integration.test",
            "profile-completion-witness-user",
            "Profile Completion"
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var registered = await api.RegisterUser(profileUser);

        Assert.Equal(profileUser.Email, registered.Email);
        Assert.Null(registered.GivenName);
        Assert.Null(registered.Surname);
        Assert.Null(registered.CountryRegion);

        var updated = await api.UpdateUserProfile(
            profileUser.Email,
            new UpdateUserProfilePayload("Petra", "Profile", "Bulgaria", club: "Konarche", feiId: "20202020")
        );
        var persisted = await api.ReadUser(profileUser.Email);

        Assert.Equal(registered.Id, updated.Id);
        Assert.Equal(profileUser.Email, updated.Email);
        Assert.Equal("Petra Profile", updated.Name);
        Assert.Equal("Petra", updated.GivenName);
        Assert.Equal("Profile", updated.Surname);
        Assert.Equal("Bulgaria", updated.CountryRegion);
        Assert.Equal("Konarche", updated.Club);
        Assert.Equal("20202020", updated.FeiId);
        Assert.NotNull(persisted);
        Assert.Equal(updated.Id, persisted!.Id);
        Assert.Equal(updated.Name, persisted.Name);
        Assert.Equal(updated.CountryRegion, persisted.CountryRegion);
    }

    [Fact]
    public async Task Witness_contexts_pick_up_the_user_when_signin_completes_after_startup()
    {
        var signingInUser = new IntegrationUser(
            "late-signin.witness@integration.test",
            "late-signin-witness-user",
            "Late Signin Witness"
        );
        await using var witness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            user: null,
            "IntegrationLateSigninWitness"
        );
        var profileContext = witness.GetRequiredService<IWitnessProfileContext>();
        var accessContext = witness.GetRequiredService<IWitnessAccessContext>();

        // The sign-in round trip hands the browser back to a freshly booted app that is still
        // anonymous, so both contexts initialize before anyone is signed in.
        await profileContext.Load();
        await accessContext.Load();

        Assert.Null(profileContext.User);
        Assert.Equal(WitnessAccessLevel.Anonymous, accessContext.AccessLevel);

        witness.SignIn(signingInUser);

        var user = await WaitForProfileUser(profileContext);
        Assert.Equal(signingInUser.Email, user.Email);
        Assert.Equal(
            WitnessAccessLevel.Registered,
            await WaitForAccessLevel(accessContext, WitnessAccessLevel.Registered)
        );
    }

    static async Task<NUserModel> WaitForProfileUser(IWitnessProfileContext profileContext)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (profileContext.User != null)
            {
                return profileContext.User;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            "Witness profile context did not pick up the signed-in user, so the drawer would keep "
                + "rendering without its profile header until a full page reload."
        );
    }

    static async Task<WitnessAccessLevel> WaitForAccessLevel(
        IWitnessAccessContext accessContext,
        WitnessAccessLevel expected
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (accessContext.AccessLevel == expected)
            {
                return accessContext.AccessLevel;
            }

            await Task.Delay(50);
        }

        return accessContext.AccessLevel;
    }

    static SetupConfigureEvent CreateOperatorSetupEvent(
        int eventId,
        SetupUser operatorUser,
        SetupUser eligibleOfficialUser,
        SetupUser ineligibleOfficialUser
    )
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var loop = new SetupLoop(20, eventId + 10);
        var phase = new SetupPhase(loop, recovery: 40, rest: null, id: eventId + 11);
        var athlete = new SetupAthlete("Operator Rider", "Operator Rider", null, country, null, eventId + 12);
        var horse = new SetupHorse("Operator Horse", "Operator Horse", null, eventId + 13);
        var combination = new SetupCombination(1, athlete, horse, eventId + 14);
        var participation = new SetupParticipation(
            false,
            combination,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            eventId + 15
        );
        var competition = new SetupCompetition(
            "Operator Access Competition",
            CompetitionRuleset.Regional,
            DateTimeOffset.UtcNow.Date.AddHours(8),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [phase],
            [participation],
            eventId + 16
        );
        var eligibleOfficial = new SetupOfficial(
            "Eligible Official",
            "Eligible Official",
            OfficialRole.GroundJuryPresident,
            eventId + 20,
            eligibleOfficialUser
        );
        var ineligibleOfficial = new SetupOfficial(
            "Ineligible Official",
            "Ineligible Official",
            OfficialRole.VeterinaryCommissionMember,
            eventId + 21,
            ineligibleOfficialUser
        );
        var @operator = new SetupOperator(operatorUser, eventId + 30);

        return new SetupConfigureEvent(
            "Operator Access Event",
            "Sofia",
            country,
            null,
            [competition],
            [eligibleOfficial, ineligibleOfficial],
            [loop],
            [combination],
            eventId,
            [@operator]
        );
    }

    static SetupUser ToSetupUser(NUserModel user)
    {
        return new SetupUser(
            user.Email,
            user.Name,
            user.Roles,
            user.Id,
            user.GivenName,
            user.MiddleName,
            user.Surname,
            user.CountryRegion,
            user.Club,
            user.FeiId,
            user.DisplayName
        );
    }

    static async Task<NtsUserSessionModel> WaitForUserSession(
        NexusApiDriver api,
        string userIdentifier,
        int eventId,
        Func<NtsUserSessionStateModel, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        NtsUserSessionModel? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await api.ReadUserSession(userIdentifier, eventId);
            if (last?.State != null && predicate(last.State))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Witness user session did not {expectedState}. {FormatUserSessionState(last?.State)}"
        );
    }

    static string FormatUserSessionState(NtsUserSessionStateModel? state)
    {
        if (state == null)
        {
            return "No session state was returned.";
        }

        var selections = string.Join(
            ", ",
            state.SnapshotSelections.Select(selection => $"#{selection.Number}@{selection.Timestamp ?? "<pending>"}")
        );
        var history = string.Join(
            ", ",
            state.SnapshotHistory.Select(group =>
                $"{group.Type}: {string.Join(", ", group.Entries.Select(entry => $"#{entry.Number}"))}"
            )
        );
        return $"Selections: [{selections}]. History: [{history}].";
    }

    static SnapshotGroup CreateSnapshotGroup()
    {
        return new SnapshotGroup(
            [new WitnessSnapshot(1, "Operator Rider", "Operator Rider", new Timestamp(DateTimeOffset.UtcNow))],
            SnapshotType.Automatic
        );
    }

    static ClaimsPrincipal CreatePrincipal(IntegrationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email),
            new("oid", user.UserIdentifier),
            new("name", user.DisplayName ?? user.Name),
        };

        AddClaim(claims, ClaimTypes.GivenName, user.GivenName);
        AddClaim(claims, "middle_name", user.MiddleName);
        AddClaim(claims, ClaimTypes.Surname, user.Surname);
        AddClaim(claims, ClaimTypes.Country, user.CountryRegion);

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "IntegrationTest"));
    }

    static void AddClaim(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(type, value));
        }
    }
}
