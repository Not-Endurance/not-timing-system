using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Events;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// Participations for the tests of the store and the views over it. The id is <c>TestId.Of(number)</c>, so two calls
/// with the same number are two versions of the same Participation: what the repository holds now, and what an event
/// carried a moment ago.
/// </summary>
internal static class ParticipationFixtures
{
    /// <summary>The notification the Api sends when this Participation changed.</summary>
    public static ParticipationChanged Changed(Participation participation)
    {
        return new ParticipationChanged(FakeSocketContext.EVENT_ID, participation.Id);
    }

    /// <summary>On its way: started, not arrived yet.</summary>
    public static Participation Active(int number)
    {
        return Create(number, CreatePhase(DateTimeOffset.Now.AddMinutes(-10), isFinal: true), eliminated: null);
    }

    /// <summary>Every Phase has arrived and presented.</summary>
    public static Participation Completed(int number)
    {
        var start = DateTimeOffset.Now.AddHours(-2);
        var arrive = start.AddHours(1);
        return Create(number, CreatePhase(start, arrive, arrive.AddMinutes(5), isFinal: true), eliminated: null);
    }

    /// <summary>Back from its Phase, not presented yet: it is on the presentlist.</summary>
    public static Participation Arrived(int number, Eliminated? eliminated = null)
    {
        var start = DateTimeOffset.Now.AddHours(-1);
        return Create(number, CreatePhase(start, DateTimeOffset.Now.AddMinutes(-5), isFinal: true), eliminated);
    }

    public static Participation Eliminated(int number)
    {
        return Create(number, CreatePhase(DateTimeOffset.Now.AddMinutes(-10), isFinal: true), new Withdrawn());
    }

    static Participation Create(int number, Phase phase, Eliminated? eliminated)
    {
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, TestId.Of(number));
        var horse = new Horse($"Horse {number}", null, null, TestId.Of(number));
        var combination = new Combination(
            number,
            athlete,
            horse,
            null,
            $"{phase.Length:0.##}",
            null,
            null,
            TestId.Of(number)
        );

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection([phase]),
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
