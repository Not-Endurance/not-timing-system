using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class UniqueParticipationsTests
{
    [Fact]
    public void Add_ignores_duplicate_numbers()
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
    public void Constructor_ignores_duplicate_numbers()
    {
        var first = CreateParticipation(7, id: 101);
        var duplicate = CreateParticipation(7, id: 202);
        var other = CreateParticipation(8, id: 303);

        var participations = new UniqueParticipations([first, duplicate, other]);

        Assert.Equal([7, 8], participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public void Upsert_replaces_existing_number()
    {
        var first = CreateParticipation(7, id: 101);
        var replacement = CreateParticipation(7, id: 202);
        var participations = new UniqueParticipations([first]);

        participations.Upsert(replacement);

        var participation = Assert.Single(participations);
        Assert.Same(replacement, participation);
    }

    [Fact]
    public void Remove_removes_matching_number()
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
            new Competition("Competition", CompetitionRuleset.Regional),
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
            CompetitionRuleset.Regional,
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
