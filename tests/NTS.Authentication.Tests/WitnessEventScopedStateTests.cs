using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using NTS.Application.Socket;
using NTS.Application.Startlists;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Witness.Contracts.Features.Core.Dashboard;
using NTS.Witness.Features.Core.Dashboard;
using CoreAthlete = NTS.Domain.Core.Aggregates.Participations.Entities.Athlete;
using CoreCombination = NTS.Domain.Core.Aggregates.Participations.Entities.Combination;
using CoreCompetition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;
using CoreHorse = NTS.Domain.Core.Aggregates.Participations.Entities.Horse;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Entities.Phase;
using CorePhaseCollection = NTS.Domain.Core.Aggregates.Participations.Objects.PhaseCollection;

namespace NTS.Authentication.Tests;

public class WitnessEventScopedStateTests
{
    [Fact]
    public async Task Performance_service_does_not_read_before_event_connection()
    {
        var socketContext = new MutableSocketContext();
        var reader = new RecordingParticipationReader([CreateParticipation(11, 77)]);
        var service = new PerformanceService(reader, socketContext);

        await service.Load();

        Assert.Equal(0, reader.ReadManyCalls);
        Assert.Empty(service.Participations);
    }

    [Fact]
    public async Task Performance_service_reads_after_event_connection()
    {
        var socketContext = new MutableSocketContext();
        var reader = new RecordingParticipationReader([CreateParticipation(12, 88)]);
        var service = new PerformanceService(reader, socketContext);

        await service.Load();
        socketContext.Event = CreateEvent(12);

        await service.Handle(new NTS.Domain.Core.Events.EventConnected(12), CancellationToken.None);

        Assert.Equal(1, reader.ReadManyCalls);
        Assert.Equal([88], service.Participations.Select(x => x.Combination.Number).ToArray());
    }

    [Fact]
    public async Task Startlist_service_does_not_read_before_event_connection_and_reloads_after_connect()
    {
        var socketContext = new MutableSocketContext();
        var reader = new RecordingParticipationReader([]);
        var service = new StartlistService(reader, socketContext);

        await service.Load();

        Assert.Equal(0, reader.ReadManyCalls);
        Assert.Empty(service.Upcoming);

        socketContext.Event = CreateEvent(19);
        await service.Handle(new EventConnected(19), CancellationToken.None);

        Assert.Equal(1, reader.ReadManyCalls);
        Assert.NotNull(service.Startlist);
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

        public int ReadManyCalls { get; private set; }

        public Task<IEnumerable<Participation>> ReadMany()
        {
            ReadManyCalls++;
            return Task.FromResult<IEnumerable<Participation>>(_participations);
        }

        public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
        {
            ReadManyCalls++;
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
}
