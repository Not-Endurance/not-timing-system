using NoTiming.Ui.Features.Core.Participations;
using NTS.Application.Startlists;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

public sealed class StartlistServiceTests
{
    [Fact]
    public async Task An_eliminated_Participation_leaves_the_upcoming_and_stays_in_the_history_and_a_restored_one_returns()
    {
        var active = CreateParticipationWithHistoryAndFutureStart(301);
        var repository = new ControlledParticipationRepository(active);
        var store = new ParticipationStore(repository);
        var service = new StartlistService(store);
        await service.Load();
        var eliminated = CreateParticipationWithHistoryAndFutureStart(301, new Withdrawn());
        repository.Store(eliminated);

        await store.Handle(ParticipationFixtures.Changed(active), CancellationToken.None);

        Assert.DoesNotContain(service.Upcoming, x => x.Number == 301);
        Assert.Contains(service.History, x => x.Number == 301);

        repository.Store(CreateParticipationWithHistoryAndFutureStart(301));
        await store.Handle(ParticipationFixtures.Changed(eliminated), CancellationToken.None);

        Assert.Contains(service.Upcoming, x => x.Number == 301);
        Assert.Contains(service.History, x => x.Number == 301);
    }

    static Participation CreateParticipationWithHistoryAndFutureStart(int number, Eliminated? eliminated = null)
    {
        var now = DateTimeOffset.Now;
        var firstStart = now.AddHours(-2);
        var firstArrive = firstStart.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(5);
        var phases = new[]
        {
            CreatePhase(firstStart, firstArrive, firstPresent),
            CreatePhase(now.AddMinutes(30), isFinal: true),
        };
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, TestId.Of(number));
        var horse = new Horse($"Horse {number}", null, null, TestId.Of(number));
        var totalDistance = phases.Sum(x => x.Length);
        var combination = new Combination(
            number,
            athlete,
            horse,
            null,
            $"{totalDistance:0.##}",
            null,
            null,
            TestId.Of(number)
        );

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phases),
            eliminated,
            eventId: TestId.Of(1),
            id: TestId.Of(number)
        );
    }

    static Phase CreatePhase(
        DateTimeOffset? start = null,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false
    )
    {
        return new Phase(
            "",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            null,
            false,
            false,
            false
        );
    }
}
