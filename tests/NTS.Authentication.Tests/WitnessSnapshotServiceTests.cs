using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using NTS.Application.Socket;
using NTS.Application.UserSession;
using NTS.Application.Watcher;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Witness.Features.Core.Dashboard;
using CoreAthlete = NTS.Domain.Core.Aggregates.Participations.Entities.Athlete;
using CoreCombination = NTS.Domain.Core.Aggregates.Participations.Entities.Combination;
using CoreCompetition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;
using CoreHorse = NTS.Domain.Core.Aggregates.Participations.Entities.Horse;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Entities.Phase;
using CorePhaseCollection = NTS.Domain.Core.Aggregates.Participations.Objects.PhaseCollection;

namespace NTS.Authentication.Tests;

public class WitnessSnapshotServiceTests
{
    [Fact]
    public async Task Remove_snapshot_returns_participation_to_available_chips()
    {
        var socketContext = new MutableSocketContext { Event = CreateEvent(11) };
        var service = new SnapshotService(
            socketContext,
            new RecordingParticipationReader([CreateParticipation(11, 77)]),
            new TestWitnessUserSession(),
            new TestSnapshotPublisher()
        );

        await service.Load();
        service.MoveToSnapshot(service.Participations.Single());

        var snapshot = Assert.Single(service.Snapshots);
        Assert.Empty(service.Participations);

        service.RemoveSnapshot(snapshot);

        Assert.Empty(service.Snapshots);
        Assert.Empty(service.ParticipationsToSnapshot);
        Assert.Equal([77], service.Participations.Select(x => x.Combination.Number).ToArray());
    }

    static Participation CreateParticipation(int eventId, int number)
    {
        var country = new Country(1000 + number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new CoreAthlete(new Person([$"Rider{number}", "Test"]), country, null, null, 2000 + number);
        var horse = new CoreHorse($"Horse{number}", null, 3000 + number);
        var combination = new CoreCombination(number, athlete, horse, null, "40", null, null, 4000 + number);
        var phase = new CorePhase(
            gate: "",
            length: 20,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.Regional,
            isFinal: false,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(DateTimeOffset.UtcNow),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 5000 + number
        );

        return new Participation(
            category: ParticipationCategory.Senior,
            competition: new CoreCompetition(
                $"Competition{number}",
                CompetitionRuleset.Regional,
                CompetitionType.Qualification
            ),
            combination: combination,
            phases: new CorePhaseCollection([phase]),
            notQualified: null,
            eventId: eventId,
            id: 6000 + number
        );
    }

    static EnduranceEvent CreateEvent(int eventId)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Ring",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            eventId
        );
    }

    sealed class RecordingParticipationReader : IReadMany<Participation>
    {
        readonly IReadOnlyList<Participation> _participations;

        public RecordingParticipationReader(IEnumerable<Participation> participations)
        {
            _participations = participations.ToList();
        }

        public Task<IEnumerable<Participation>> ReadMany()
        {
            return Task.FromResult<IEnumerable<Participation>>(_participations);
        }

        public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<Participation>>(_participations.Where(predicate).ToList());
        }
    }

    sealed class MutableSocketContext : INtsSocketContext
    {
        public bool IsConnected => Event != null;
        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }
    }

    sealed class TestWitnessUserSession : IWitnessUserSession
    {
        public Task<NtsUserSessionStateModel?> GetCurrent()
        {
            return Task.FromResult<NtsUserSessionStateModel?>(null);
        }

        public Task SetEventId(int? eventId)
        {
            return Task.CompletedTask;
        }

        public Task AppendSnapshot(SnapshotGroup snapshot)
        {
            return Task.CompletedTask;
        }

        public Task DeleteCurrent()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestSnapshotPublisher : ISnapshotPublisher
    {
        public Task PublishSnapshotsAsync(SnapshotGroup snapshoutGroup)
        {
            return Task.CompletedTask;
        }
    }
}
