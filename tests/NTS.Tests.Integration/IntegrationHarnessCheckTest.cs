using System.Security.Claims;
using Not.Application.Authentication.User;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using NTS.Witness.Contracts.API;
using NTS.Witness.Contracts.Features.Access;
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
using WitnessSnapshot = NTS.Domain.Watcher.Snapshot;

namespace NTS.Tests.Integration;

public sealed class IntegrationHarnessCheckTest : IClassFixture<NtsIntegrationFixture>
{
    static readonly IntegrationUser OFFICIAL_USER = new(
        "official.witness@integration.test",
        "official-witness-user",
        "Official Witness"
    );
    static readonly IntegrationUser PARTICIPANT_USER = new(
        "participant.witness@integration.test",
        "participant-witness-user",
        "Participant Witness"
    );

    readonly NtsIntegrationFixture _fixture;

    public IntegrationHarnessCheckTest(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Judge_snapshot_flow_updates_connected_witness_applications()
    {
        var eventId = 1701;
        var participationNumber = 42;
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId);
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, participationNumber);
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var officialUser = await api.RegisterUser(OFFICIAL_USER);
        await api.RegisterUser(PARTICIPANT_USER);
        await api.Create(eventInformation);
        await api.Create(participation);
        await api.Create(IntegrationPayloadFactory.Official(eventId, officialUser.Id));
        var seededParticipation = await api.ReadParticipation(eventId, participation.Id);
        var seededParticipations = await api.ReadParticipations(eventId);
        var seededParticipationsRaw = await api.ReadParticipationsRaw(eventId);
        Assert.Equal(participationNumber, seededParticipation.Combination.Number);
        Assert.True(
            seededParticipations.Any(x => x.Combination.Number == participationNumber),
            $"Nexus API list endpoint did not return participation #{participationNumber}. Raw response: {seededParticipationsRaw}"
        );

        await using var judge = new JudgeDriver(_fixture.WarpBaseUrl, _fixture.NexusBaseUrl);
        await using var officialWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            OFFICIAL_USER,
            "IntegrationOfficialWitness"
        );
        await using var participantWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            PARTICIPANT_USER,
            "IntegrationParticipantWitness"
        );

        await judge.Start();
        await officialWitness.Start();
        await participantWitness.Start();

        await officialWitness.Connect(eventInformation);
        await participantWitness.Connect(eventInformation);
        await judge.Connect(eventInformation);
        var judgeRepositoryParticipations = await judge.ReadParticipations();
        Assert.True(
            judgeRepositoryParticipations.Any(x => x.Combination.Number == participationNumber),
            $"Judge repository did not return participation #{participationNumber}. Count: {judgeRepositoryParticipations.Count}, repository: {judge.ParticipationRepositoryType}, http: {judge.HttpBaseUrl}."
        );

        await judge.Record(
            IntegrationPayloadFactory.AutomaticSnapshot(participationNumber, DateTimeOffset.UtcNow.Date.AddHours(10))
        );
        await judge.Record(
            IntegrationPayloadFactory.AutomaticSnapshot(
                participationNumber,
                DateTimeOffset.UtcNow.Date.AddHours(10).AddMinutes(5)
            )
        );

        var judgeParticipation = judge.Participations.FirstOrDefault(x => x.Combination.Number == participationNumber);
        Assert.True(
            judgeParticipation?.Phases.Current.IsComplete() == true,
            $"Judge did not complete participation #{participationNumber}. Loaded participations: {judge.Participations.Count}, recently timed: {string.Join(", ", judge.RecentlyTimed)}, repository: {judge.ParticipationRepositoryType}, http: {judge.HttpBaseUrl}."
        );

        var persistedParticipation = await api.WaitForParticipation(
            eventId,
            participation.Id,
            received => received.Phases.Current.IsComplete(),
            TimeSpan.FromSeconds(10)
        );

        var officialReceived = await officialWitness.WaitForParticipation(
            participationNumber,
            received => received.Phases.Current.IsComplete(),
            TimeSpan.FromSeconds(10)
        );
        var participantReceived = await participantWitness.WaitForParticipation(
            participationNumber,
            received => received.Phases.Current.IsComplete(),
            TimeSpan.FromSeconds(10)
        );

        Assert.Equal(42, officialReceived.Combination.Number);
        Assert.Equal(eventId, officialReceived.EventId);
        Assert.Equal(42, participantReceived.Combination.Number);
        Assert.Equal(eventId, participantReceived.EventId);
        Assert.Equal(WitnessAccessLevel.Official, officialWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Participant, participantWitness.AccessLevel);

        var persistedSnapshotResults = await api.ReadSnapshotResults(eventId);

        Assert.True(persistedParticipation.Phases.Current.IsComplete());
        Assert.Equal(2, persistedSnapshotResults.Count);
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
        var participantIdentity = new IntegrationUser(
            "operator-participant.witness@integration.test",
            "operator-participant-witness-user",
            "Operator Participant Witness"
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var operatorUser = ToSetupUser(await api.RegisterUser(operatorIdentity));
        var eligibleOfficialUser = ToSetupUser(await api.RegisterUser(eligibleOfficialIdentity));
        var ineligibleOfficialUser = ToSetupUser(await api.RegisterUser(ineligibleOfficialIdentity));
        await api.RegisterUser(participantIdentity);

        var setupEvent = CreateOperatorSetupEvent(
            eventId,
            operatorUser,
            eligibleOfficialUser,
            ineligibleOfficialUser
        );
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
        await using var participantWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            participantIdentity,
            "IntegrationOperatorParticipantWitness"
        );

        await operatorWitness.Start();
        await eligibleOfficialWitness.Start();
        await ineligibleOfficialWitness.Start();
        await participantWitness.Start();

        await operatorWitness.Connect(eventInformation);
        await eligibleOfficialWitness.Connect(eventInformation);
        await ineligibleOfficialWitness.Connect(eventInformation);
        await participantWitness.Connect(eventInformation);

        Assert.Equal(WitnessAccessLevel.Official, operatorWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Official, eligibleOfficialWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Participant, ineligibleOfficialWitness.AccessLevel);
        Assert.Equal(WitnessAccessLevel.Participant, participantWitness.AccessLevel);

        await operatorWitness.Publish(CreateSnapshotGroup());
        await eligibleOfficialWitness.Publish(CreateSnapshotGroup());
        var denied = await Assert.ThrowsAnyAsync<Exception>(() =>
            ineligibleOfficialWitness.Publish(CreateSnapshotGroup())
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
