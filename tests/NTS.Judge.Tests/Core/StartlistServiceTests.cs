using Not.Application.CRUD.Ports;
using NTS.Application.Startlists;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Events;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Judge.Tests.Core;

public class StartlistServiceTests
{
    [Fact]
    public async Task Handle_WhenEventConnectedIsRaisedAfterParticipationsExist_ReloadsUpcomingEntries()
    {
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);

        await service.Load();

        Assert.Empty(service.Upcoming);

        repository.Items = [CreateParticipation(1, 77, DateTimeOffset.Now.AddMinutes(10))];

        await service.Handle(new EventConnected(14), CancellationToken.None);

        Assert.Equal([77], service.Upcoming.Select(x => x.Number).ToArray());
    }

    static Participation CreateParticipation(int id, int number, DateTimeOffset phase1Start)
    {
        var country = new Country(1000 + id, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person([$"Rider{id}", "Test"]), country, null, null, 2000 + id);
        var horse = new Horse($"Horse{id}", null, 3000 + id);
        var combination = new Combination(number, athlete, horse, null, "40", null, null, 4000 + id);

        var phase = new Phase(
            gate: "",
            length: 20,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.Regional,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(phase1Start),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 5000 + id * 10 + 1
        );

        return new Participation(
            category: ParticipationCategory.Senior,
            competition: new Competition(
                $"Competition{id}",
                CompetitionRuleset.Regional,
                CompetitionType.Qualification
            ),
            combination: combination,
            phases: new PhaseCollection([phase]),
            notQualified: null,
            eventId: 7000 + id,
            id: 6000 + id
        );
    }

    sealed class MutableParticipationRepository : IReadMany<Participation>
    {
        public MutableParticipationRepository(IEnumerable<Participation> items)
        {
            Items = items.ToList();
        }

        public List<Participation> Items { get; set; }

        public Task<IEnumerable<Participation>> ReadMany()
        {
            return Task.FromResult<IEnumerable<Participation>>(Items.ToList());
        }

        public Task<IEnumerable<Participation>> ReadMany(System.Linq.Expressions.Expression<Func<Participation, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<Participation>>(Items.Where(predicate).ToList());
        }
    }
}
