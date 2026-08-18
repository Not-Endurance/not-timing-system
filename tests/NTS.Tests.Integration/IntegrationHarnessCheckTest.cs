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
using NTS.Domain.Watcher;
using NTS.Judge.Contracts.Features.Core.Dashboard;
using NTS.Judge.Contracts.Features.Core.Handouts;
using NTS.Localization;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using NTS.Witness.Contracts.API;
using NTS.Witness.Contracts.Features.Access;
using NTS.Witness.Contracts.Features.Performance;
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
using WitnessSnapshotService = NTS.Witness.Contracts.Features.Snapshots.ISnapshotService;

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
        var arrivelistParticipationNumber = 43;
        var arrivelistStart = DateTimeOffset.UtcNow.AddHours(-3);
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId);
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, participationNumber);
        var arrivelistParticipation = IntegrationPayloadFactory.ActiveParticipation(
            eventId,
            arrivelistParticipationNumber,
            id: 5501,
            minAverageSpeed: 10,
            maxAverageSpeed: 20,
            startTime: arrivelistStart
        );
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var officialUser = await api.RegisterUser(OFFICIAL_USER);
        await api.RegisterUser(PARTICIPANT_USER);
        await api.Create(eventInformation);
        await api.Create(participation);
        await api.Create(arrivelistParticipation);
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
        var officialArrivelist = officialWitness.GetRequiredService<IArrivelistService>();
        await officialArrivelist.Load();
        Assert.Contains(officialArrivelist.Entries, x => x.Number == arrivelistParticipationNumber);

        var judgeRepositoryParticipations = await judge.ReadParticipations();
        Assert.True(
            judgeRepositoryParticipations.Any(x => x.Combination.Number == participationNumber),
            $"Judge repository did not return participation #{participationNumber}. Count: {judgeRepositoryParticipations.Count}, repository: {judge.ParticipationRepositoryType}, http: {judge.HttpBaseUrl}."
        );

        await judge.Record(
            IntegrationPayloadFactory.AutomaticSnapshot(arrivelistParticipationNumber, arrivelistStart.AddHours(2))
        );
        await WaitForArrivelist(
            officialArrivelist,
            entries => entries.All(x => x.Number != arrivelistParticipationNumber),
            $"remove participation #{arrivelistParticipationNumber} after arrival"
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

        var performanceParticipations = officialWitness.GetRequiredService<IPerformanceParticipations>();
        if (performanceParticipations is NStatefulService performanceStateful)
        {
            performanceStateful.ResetHasLoaded();
        }

        await performanceParticipations.Load();
        Assert.Contains(
            performanceParticipations.Participations,
            x => x.Combination.Number == participationNumber && x.Phases.Current.IsComplete()
        );

        var snapshots = officialWitness.GetRequiredService<WitnessSnapshotService>();
        if (snapshots is NStatefulService snapshotsStateful)
        {
            snapshotsStateful.ResetHasLoaded();
        }

        await snapshots.Load();
        Assert.DoesNotContain(snapshots.Participations, x => x.Combination.Number == participationNumber);

        var persistedSnapshotResults = await api.ReadSnapshotResults(eventId);

        Assert.True(persistedParticipation.Phases.Current.IsComplete());
        Assert.Equal(3, persistedSnapshotResults.Count);
    }

    [Fact]
    public async Task Judge_handouts_follow_phase_completion_rules_and_snapshot_keeps_selection()
    {
        var eventId = 1951;
        var firstNumber = 71;
        var secondNumber = 72;
        var manualNumber = 73;
        var start = DateTimeOffset.UtcNow.Date.AddHours(8);
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId);
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        await api.Create(eventInformation);
        await api.Create(
            IntegrationPayloadFactory.TwoPhaseParticipation(eventId, firstNumber, id: 5901, startTime: start)
        );
        await api.Create(
            IntegrationPayloadFactory.TwoPhaseParticipation(eventId, secondNumber, id: 5902, startTime: start)
        );
        await api.Create(
            IntegrationPayloadFactory.ActiveParticipation(eventId, manualNumber, id: 5903, startTime: start)
        );

        await using var judge = new JudgeDriver(_fixture.WarpBaseUrl, _fixture.NexusBaseUrl);
        await judge.Start();
        await judge.Connect(eventInformation);

        var context = judge.GetRequiredService<IParticipationContext>();
        if (context is NStatefulService stateful)
        {
            stateful.ResetHasLoaded();
        }

        await context.Load();
        var firstLoaded = context.Participations.First();
        var selectedParticipation = context.Participations.First(x => x.Id != firstLoaded.Id && x.Phases.Count > 1);
        context.Selected = selectedParticipation;
        var selectedId = selectedParticipation.Id;
        var selectedNumber = selectedParticipation.Combination.Number;

        var firstArrival = start.AddMinutes(30);
        var firstPresentation = firstArrival.AddMinutes(5);
        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(selectedNumber, firstArrival));
        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(selectedNumber, firstPresentation));

        Assert.Equal(selectedId, context.Selected?.Id);
        var nonFinalHandouts = await WaitForHandouts(
            api,
            eventId,
            handouts => HandoutsForNumber(handouts, selectedNumber).Count == 1,
            $"create a non-final handout for #{selectedNumber}"
        );
        var nonFinalHandout = Assert.Single(HandoutsForNumber(nonFinalHandouts, selectedNumber));

        var finalArrival = firstPresentation.AddMinutes(75);
        var finalPresentation = finalArrival.AddMinutes(5);
        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(selectedNumber, finalArrival));
        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(selectedNumber, finalPresentation));

        Assert.Equal(selectedId, context.Selected?.Id);
        await api.WaitForParticipation(
            eventId,
            selectedId,
            participation => participation.Phases.Current.IsComplete(),
            TimeSpan.FromSeconds(10)
        );
        var afterFinalHandouts = await WaitForHandouts(
            api,
            eventId,
            handouts =>
            {
                var selectedHandouts = HandoutsForNumber(handouts, selectedNumber);
                return selectedHandouts.Count == 1 && selectedHandouts.Single().Id == nonFinalHandout.Id;
            },
            $"keep only the existing non-final handout for #{selectedNumber}"
        );

        var manualHandouts = judge.GetRequiredService<ICreateHandout>();
        await manualHandouts.Create(manualNumber);

        await WaitForHandouts(
            api,
            eventId,
            handouts =>
            {
                var selectedHandouts = HandoutsForNumber(handouts, selectedNumber);
                return selectedHandouts.Count == 1
                    && selectedHandouts.Single().Id == HandoutsForNumber(afterFinalHandouts, selectedNumber).Single().Id
                    && HandoutsForNumber(handouts, manualNumber).Count == 1;
            },
            $"create a manual handout for #{manualNumber}"
        );
        Assert.Equal(selectedId, context.Selected?.Id);
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
    public async Task Presentlist_updates_from_judge_events_on_every_connected_witness()
    {
        var eventId = 1702;
        var presentNumber = 51;
        var representNumber = 52;
        var riNumber = 53;
        var criNumber = 54;
        var baseTime = DateTimeOffset.UtcNow.Date.AddHours(10);
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId);
        using var api = new NexusApiDriver(_fixture.NexusBaseUrl);

        var officialUser = await api.RegisterUser(OFFICIAL_USER);
        await api.RegisterUser(PARTICIPANT_USER);
        await api.Create(eventInformation);
        await api.Create(
            IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                presentNumber,
                id: 5601,
                startTime: baseTime.AddHours(-1)
            )
        );
        await api.Create(
            IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                representNumber,
                id: 5602,
                startTime: baseTime.AddHours(-1)
            )
        );
        await api.Create(
            IntegrationPayloadFactory.TwoPhaseParticipation(
                eventId,
                riNumber,
                id: 5603,
                startTime: baseTime.AddHours(-1)
            )
        );
        await api.Create(
            IntegrationPayloadFactory.TwoPhaseParticipation(
                eventId,
                criNumber,
                id: 5604,
                compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
                startTime: baseTime.AddHours(-1)
            )
        );
        await api.Create(IntegrationPayloadFactory.Official(eventId, officialUser.Id, id: 6601));

        await using var judge = new JudgeDriver(_fixture.WarpBaseUrl, _fixture.NexusBaseUrl);
        await using var officialWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            OFFICIAL_USER,
            "PresentlistOfficialWitness"
        );
        await using var participantWitness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            PARTICIPANT_USER,
            "PresentlistParticipantWitness"
        );

        await judge.Start();
        await officialWitness.Start();
        await participantWitness.Start();

        await officialWitness.Connect(eventInformation);
        await participantWitness.Connect(eventInformation);
        await judge.Connect(eventInformation);

        var officialPresentlist = officialWitness.GetRequiredService<IPresentlistService>();
        var participantPresentlist = participantWitness.GetRequiredService<IPresentlistService>();
        await officialPresentlist.Load();
        await participantPresentlist.Load();

        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(presentNumber, baseTime));
        var presentEntry = await WaitForPresentlistEntry(
            officialPresentlist,
            presentNumber,
            PresentlistEntryType.Present,
            "show a Present entry after arrival"
        );
        Assert.Equal(baseTime.AddMinutes(40), presentEntry.Time.ToDateTimeOffset());
        await WaitForPresentlistEntry(
            participantPresentlist,
            presentNumber,
            PresentlistEntryType.Present,
            "show a Present entry on another connected Witness"
        );

        await RecordArrivalAndPresentation(judge, representNumber, baseTime.AddMinutes(10), TimeSpan.FromMinutes(5));
        await SelectJudgeParticipation(judge, representNumber);
        await judge.GetRequiredService<IInspectionService>().RequestRepresent(true);
        var pendingRepresentationInspectionException = await Assert.ThrowsAnyAsync<Exception>(
            () => judge.GetRequiredService<IInspectionService>().RequestInspection(true)
        );
        Assert.Equal(
            nameof(NtsStrings.Cannot_request_Required_Inspection_without_Representation_time_string),
            pendingRepresentationInspectionException.Message
        );
        Assert.Equal(
            "Cannot request Required Inspection without Representation time",
            judge.GetRequiredService<IStringLocalizer>()[pendingRepresentationInspectionException.Message].Value
        );
        var pendingRepresentation = await api.ReadParticipation(eventId, 5602);
        Assert.False(pendingRepresentation.Phases.Current.IsRequiredInspectionRequested);

        await WaitForPresentlistEntry(
            officialPresentlist,
            representNumber,
            PresentlistEntryType.Represent,
            "show a Represent entry after representation is requested"
        );
        await WaitForPresentlistEntry(
            participantPresentlist,
            representNumber,
            PresentlistEntryType.Represent,
            "show a Represent entry on another connected Witness"
        );

        await RecordArrivalAndPresentation(judge, riNumber, baseTime.AddMinutes(20), TimeSpan.FromMinutes(5));
        await SelectJudgeParticipation(judge, riNumber);
        await judge.GetRequiredService<IInspectionService>().RequestInspection(true);

        await RecordArrivalAndPresentation(judge, criNumber, baseTime.AddMinutes(30), TimeSpan.FromMinutes(20));

        await WaitForPresentlistEntry(
            officialPresentlist,
            riNumber,
            PresentlistEntryType.RI,
            "show an RI entry after required inspection is requested"
        );
        await WaitForPresentlistEntry(
            officialPresentlist,
            criNumber,
            PresentlistEntryType.CRI,
            "show a CRI entry after compulsory inspection is calculated"
        );
        await WaitForPresentlistEntry(
            participantPresentlist,
            riNumber,
            PresentlistEntryType.RI,
            "show an RI entry on another connected Witness"
        );
        await WaitForPresentlistEntry(
            participantPresentlist,
            criNumber,
            PresentlistEntryType.CRI,
            "show a CRI entry on another connected Witness"
        );

        await participantWitness.Disconnect();
        await participantWitness.Connect(eventInformation);

        await WaitForPresentlist(
            participantPresentlist,
            entries =>
                ContainsPresentlistEntry(entries, presentNumber, PresentlistEntryType.Present)
                && ContainsPresentlistEntry(entries, representNumber, PresentlistEntryType.Represent)
                && ContainsPresentlistEntry(entries, riNumber, PresentlistEntryType.RI)
                && ContainsPresentlistEntry(entries, criNumber, PresentlistEntryType.CRI),
            "rebuild every entry from persisted state after reconnect"
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

    static async Task RecordArrivalAndPresentation(
        JudgeDriver judge,
        int number,
        DateTimeOffset arrival,
        TimeSpan recovery
    )
    {
        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(number, arrival));
        await judge.Record(IntegrationPayloadFactory.AutomaticSnapshot(number, arrival.Add(recovery)));
    }

    static async Task SelectJudgeParticipation(JudgeDriver judge, int number)
    {
        var context = judge.GetRequiredService<IParticipationContext>();
        if (context is NStatefulService stateful)
        {
            stateful.ResetHasLoaded();
        }

        await context.Load();
        context.Selected = context.Participations.Single(x => x.Combination.Number == number);
    }

    static async Task<PresentlistEntry> WaitForPresentlistEntry(
        IPresentlistService presentlist,
        int number,
        PresentlistEntryType type,
        string expectedState
    )
    {
        PresentlistEntry? entry = null;
        await WaitForPresentlist(
            presentlist,
            entries =>
            {
                entry = entries.SingleOrDefault(x => x.Number == number && x.Type == type);
                return entry != null;
            },
            expectedState
        );

        return entry!;
    }

    static async Task WaitForPresentlist(
        IPresentlistService presentlist,
        Func<IReadOnlyList<PresentlistEntry>, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<PresentlistEntry> last = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            await presentlist.Load();
            last = presentlist.Entries;
            if (predicate(last))
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Witness Presentlist did not {expectedState}. Entries: {FormatPresentlistEntries(last)}."
        );
    }

    static bool ContainsPresentlistEntry(IEnumerable<PresentlistEntry> entries, int number, PresentlistEntryType type)
    {
        return entries.Any(x => x.Number == number && x.Type == type);
    }

    static string FormatPresentlistEntries(IEnumerable<PresentlistEntry> entries)
    {
        return string.Join(", ", entries.Select(x => $"{x.Number}:{x.Type}@{x.Time}"));
    }

    static async Task WaitForArrivelist(
        IArrivelistService arrivelist,
        Func<IReadOnlyList<ArrivelistEntry>, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<ArrivelistEntry> last = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = arrivelist.Entries;
            if (predicate(last))
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Witness Arrivelist did not {expectedState}. Entries: {string.Join(", ", last.Select(x => x.Number))}."
        );
    }

    static async Task<IReadOnlyList<Handout>> WaitForHandouts(
        NexusApiDriver api,
        int eventId,
        Func<IReadOnlyList<Handout>, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<Handout> last = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await api.ReadHandouts(eventId);
            if (predicate(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Handouts did not {expectedState}. Handout count: {last.Count}.");
    }

    static IReadOnlyList<Handout> HandoutsForNumber(IEnumerable<Handout> handouts, int number)
    {
        return handouts
            .Where(handout => handout.Entries.Any(entry => entry.Participation.Combination.Number == number))
            .ToArray();
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
