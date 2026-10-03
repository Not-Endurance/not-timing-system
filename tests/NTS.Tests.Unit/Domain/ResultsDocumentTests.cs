using Not.Exceptions;
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
        var ranking = CreateRanking([new RankingEntry(second.Id, false), new RankingEntry(first.Id, false)]);

        var results = new Result(ranking, [first, second]);
        var document = new ResultsDocument(results, CreateEvent(), []);

        Assert.True(document.IsRanked);
        Assert.Equal(TestId.Of(99), document.Id);
        Assert.Equal(TestId.Of(99), document.Results.RankingId);
        Assert.Equal([first.Id, second.Id], document.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], document.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Results_put_eliminated_last_then_not_ranked_then_incomplete_and_rank_the_rest_by_arrival()
    {
        var now = DateTimeOffset.Now;
        var early = CreateParticipation(1, now.AddHours(-4), now.AddHours(-3));
        var late = CreateParticipation(5, now.AddHours(-3), now.AddHours(-2));
        var incomplete = CreateParticipation(4, now.AddHours(-4), now.AddHours(-3), presented: false);
        var notRanked = CreateParticipation(2, now.AddHours(-5), now.AddHours(-4));
        var eliminated = CreateParticipation(3, now.AddHours(-5), now.AddHours(-4), new Withdrawn());
        var ranking = CreateRanking(
            [
                new RankingEntry(eliminated.Id, false),
                new RankingEntry(notRanked.Id, true),
                new RankingEntry(late.Id, false),
                new RankingEntry(incomplete.Id, false),
                new RankingEntry(early.Id, false),
            ]
        );

        var document = new ResultsDocument(
            new Result(ranking, [early, late, incomplete, notRanked, eliminated]),
            CreateEvent(),
            []
        );

        Assert.Equal(
            [early.Id, late.Id, incomplete.Id, notRanked.Id, eliminated.Id],
            document.Entries.Select(x => x.ParticipationId)
        );
        Assert.Equal([1, 2, 3, 4, 5], document.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Single_entry_ranking_results_preserve_existing_rank()
    {
        var participation = CreateParticipation(12, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var ranking = CreateRanking([new RankingEntry(participation.Id, false, 7)]);

        var results = new Result(ranking, [participation]);
        var document = new ResultsDocument(results, CreateEvent(), []);

        Assert.False(document.IsRanked);
        Assert.Equal(TestId.Of(99), document.Results.RankingId);
        var entry = Assert.Single(document.Entries);
        Assert.Equal(7, entry.Rank);
    }

    [Fact]
    public void A_Participation_counted_in_two_Rankings_keeps_a_separate_not_ranked_mark_in_each()
    {
        var shared = CreateParticipation(1, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var other = CreateParticipation(2, DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
        var regular = CreateRanking([new RankingEntry(shared.Id, false), new RankingEntry(other.Id, false)]);
        var custom = CreateRanking(
            [new RankingEntry(shared.Id, true), new RankingEntry(other.Id, false)],
            TestId.Of(98)
        );

        var inTheRegularOne = new Result(regular, [shared, other]);
        var inTheCustomOne = new Result(custom, [shared, other]);

        Assert.False(inTheRegularOne.Entries.Single(x => x.ParticipationId == shared.Id).IsNotRanked);
        Assert.True(inTheCustomOne.Entries.Single(x => x.ParticipationId == shared.Id).IsNotRanked);
        Assert.Equal([shared.Id, other.Id], inTheRegularOne.Entries.Select(x => x.ParticipationId));
        Assert.Equal([other.Id, shared.Id], inTheCustomOne.Entries.Select(x => x.ParticipationId));
    }

    [Fact]
    public void Composing_a_Ranking_whose_Participation_is_not_there_fails_and_names_it()
    {
        var present = CreateParticipation(1, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var ranking = CreateRanking([new RankingEntry(present.Id, false), new RankingEntry(TestId.Of(2), false)]);

        var missing = Assert.Throws<GuardException>(() => new Result(ranking, [present]));

        Assert.Contains(TestId.Of(2).ToString(), missing.Message);
    }

    [Fact]
    public void Handout_results_use_unranked_single_participation_shape()
    {
        var participation = CreateParticipation(12, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var handout = new Handout(participation.EventId, participation.Id, id: TestId.Of(42));

        var document = new ResultsDocument(new Result(handout, participation), CreateEvent(), []);

        Assert.False(document.IsRanked);
        Assert.Equal(TestId.Of(42), document.Id);
        Assert.Null(document.Results.RankingId);
        Assert.Equal("Competition", document.Header.Title);
        var entry = Assert.Single(document.Entries);
        Assert.Same(participation, entry.Participation);
        Assert.Null(entry.Rank);
    }

    [Fact]
    public void A_Handout_holds_ids_only_so_its_document_shows_a_time_corrected_after_the_Handout_was_created()
    {
        var start = DateTimeOffset.Now.AddHours(-2);
        var arrive = DateTimeOffset.Now.AddHours(-1);
        var participation = CreateParticipation(12, start, arrive);
        var handout = new Handout(participation.EventId, participation.Id, id: TestId.Of(42));
        var shownBefore = ArriveTimeShownBy(new Result(handout, participation));

        participation.Update(
            new CorrectedPhase(participation.Phases.Single().Id, start, arrive.AddMinutes(7), arrive.AddMinutes(12))
        );
        var shownAfter = ArriveTimeShownBy(new Result(handout, participation));

        Assert.Equal(participation.Id, handout.ParticipationId);
        Assert.Equal(participation.EventId, handout.EventId);
        Assert.Equal(new Timestamp(arrive), shownBefore);
        Assert.Equal(new Timestamp(arrive.AddMinutes(7)), shownAfter);
    }

    [Fact]
    public void A_Handout_cannot_be_composed_from_another_Participation()
    {
        var participation = CreateParticipation(12, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var another = CreateParticipation(13, DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1));
        var handout = new Handout(participation.EventId, participation.Id);

        var mismatch = Assert.Throws<GuardException>(() => new Result(handout, another));

        Assert.Contains(another.Id.ToString(), mismatch.Message);
        Assert.Contains(participation.Id.ToString(), mismatch.Message);
    }

    static Timestamp? ArriveTimeShownBy(Result results)
    {
        return results.Entries.Single().Participation.Phases.Single().ArriveTime;
    }

    static Ranking CreateRanking(IEnumerable<RankingEntry> entries, Guid? id = null)
    {
        return new Ranking(
            "CEI 1*",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId: TestId.Of(10),
            id: id ?? TestId.Of(99)
        );
    }

    static EventInformation CreateEvent()
    {
        return new EventInformation(
            CreateCountry(),
            "Event",
            "Location",
            new EventSpan(DateTimeOffset.Now.Date, DateTimeOffset.Now.Date.AddDays(1)),
            null,
            id: TestId.Of(10)
        );
    }

    static Participation CreateParticipation(
        int number,
        DateTimeOffset start,
        DateTimeOffset arrive,
        Eliminated? eliminated = null,
        bool presented = true
    )
    {
        var country = CreateCountry();
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, TestId.Of(number));
        var horse = new Horse($"Horse {number}", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection([CreateCompletePhase(start, arrive, presented)]),
            eliminated,
            eventId: TestId.Of(10),
            id: TestId.Of(number)
        );
    }

    static Phase CreateCompletePhase(DateTimeOffset start, DateTimeOffset arrive, bool presented = true)
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
            presented ? Timestamp.Create(arrive.AddMinutes(5)) : null,
            null,
            false,
            false,
            false
        );
    }

    static Country CreateCountry()
    {
        return new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
    }

    sealed class CorrectedPhase : IPhaseState
    {
        public CorrectedPhase(Guid id, DateTimeOffset startTime, DateTimeOffset arriveTime, DateTimeOffset presentTime)
        {
            Id = id;
            StartTime = startTime;
            ArriveTime = arriveTime;
            PresentTime = presentTime;
        }

        public Guid Id { get; }
        public DateTimeOffset? StartTime { get; }
        public DateTimeOffset? ArriveTime { get; }
        public DateTimeOffset? PresentTime { get; }
        public DateTimeOffset? RepresentTime => null;
    }
}
