using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Aggregates.Participations.Objects;

public sealed class UniqueParticipationsTests
{
    [Fact]
    public void Add_WhenNumberAlreadyExists_ShouldIgnoreDuplicate()
    {
        var first = CreateParticipation(7, id: 101);
        var duplicate = CreateParticipation(7, id: 202);
        var participations = new UniqueParticipations();

        participations.Add(first);
        participations.Add(duplicate);

        var participation = Assert.Single(participations);
        Assert.Same(first, participation);
    }

    [Fact]
    public void Constructor_WhenNumbersAreDuplicated_ShouldIgnoreDuplicates()
    {
        var first = CreateParticipation(7, id: 101);
        var duplicate = CreateParticipation(7, id: 202);
        var other = CreateParticipation(8, id: 303);

        var participations = new UniqueParticipations([first, duplicate, other]);

        Assert.Equal([7, 8], participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public void Upsert_WhenNumberAlreadyExists_ShouldReplaceParticipation()
    {
        var first = CreateParticipation(7, id: 101);
        var replacement = CreateParticipation(7, id: 202);
        var participations = new UniqueParticipations([first]);

        participations.Upsert(replacement);

        var participation = Assert.Single(participations);
        Assert.Same(replacement, participation);
    }

    [Fact]
    public void Remove_WhenNumberMatches_ShouldRemoveParticipation()
    {
        var first = CreateParticipation(7, id: 101);
        var sameNumber = CreateParticipation(7, id: 202);
        var other = CreateParticipation(8, id: 303);
        var participations = new UniqueParticipations([first, other]);

        Assert.True(participations.Remove(sameNumber));

        Assert.Equal([8], participations.Select(x => x.Combination.Number));
    }

    static Participation CreateParticipation(int number, int id)
    {
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var combination = new Combination(number, athlete, horse, null, "20", null, null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", false),
            combination,
            new PhaseCollection([CreatePhase()]),
            null,
            eventId: 1,
            id
        );
    }

    static Phase CreatePhase()
    {
        return new Phase(
            "",
            20,
            40,
            null,
            true,
            null,
            Timestamp.Create(DateTimeOffset.Now),
            null,
            null,
            null,
            false,
            false,
            false
        );
    }
}
