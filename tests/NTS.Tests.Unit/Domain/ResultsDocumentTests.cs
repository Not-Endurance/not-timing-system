using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class ResultsDocumentTests
{
    [Fact]
    public void Results_document_preserves_ranked_entries()
    {
        var first = CreateParticipation(1, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var second = CreateParticipation(2, DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
        var ranking = new Ranking(
            "CEI 1*",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(second, null, false), new RankingEntry(first, null, false)],
            eventId: 10,
            id: 99
        );

        var results = new Result(ranking);
        var document = new ResultsDocument(results, CreateEvent(), []);

        Assert.True(document.IsRanked);
        Assert.Equal(99, document.Id);
        Assert.Equal(99, document.Results.RankingId);
        Assert.Equal([first.Id, second.Id], document.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], document.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Single_entry_ranking_results_preserve_existing_rank()
    {
        var participation = CreateParticipation(12, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var ranking = new Ranking(
            "CEI 1*",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(participation, 7, false)],
            eventId: 10,
            id: 99
        );

        var results = new Result(ranking);
        var document = new ResultsDocument(results, CreateEvent(), []);

        Assert.False(document.IsRanked);
        Assert.Equal(99, document.Results.RankingId);
        var entry = Assert.Single(document.Entries);
        Assert.Equal(7, entry.Rank);
    }

    [Fact]
    public void Handout_results_use_unranked_single_participation_shape()
    {
        var participation = CreateParticipation(12, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var handout = new Handout(participation, id: 42);

        var document = new ResultsDocument(handout, CreateEvent(), []);

        Assert.False(document.IsRanked);
        Assert.Equal(42, document.Id);
        Assert.Null(document.Results.RankingId);
        Assert.Equal("Competition", document.Header.Title);
        var entry = Assert.Single(document.Entries);
        Assert.Same(participation, entry.Participation);
        Assert.Null(entry.Rank);
    }

    static EventInformation CreateEvent()
    {
        return new EventInformation(
            CreateCountry(),
            "Event",
            "Location",
            new EventSpan(DateTimeOffset.Now.Date, DateTimeOffset.Now.Date.AddDays(1)),
            null,
            id: 10
        );
    }

    static Participation CreateParticipation(int number, DateTimeOffset start, DateTimeOffset arrive)
    {
        var country = CreateCountry();
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var combination = new Combination(number, athlete, horse, null, "20", null, null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection([CreateCompletePhase(start, arrive)]),
            null,
            eventId: 10,
            id: number
        );
    }

    static Phase CreateCompletePhase(DateTimeOffset start, DateTimeOffset arrive)
    {
        return new Phase(
            "GATE1",
            20,
            40,
            null,
            CompetitionRuleset.Regional,
            true,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(arrive.AddMinutes(5)),
            null,
            false,
            false,
            false
        );
    }

    static Country CreateCountry()
    {
        return new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
    }
}
