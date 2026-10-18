using System.Globalization;
using Not.Exceptions;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Core.Models;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// Composes the history of an Athlete or a Horse (#638, ADR-0010, ADR-0006, ADR-0007): a line for each ride of an Event that has
/// ended, in any Tenant, with what the ride placed as. The copies a Participation keeps carry the id of the registry row they
/// come from, so the id finds the rides whatever Tenant ran them. An Event that is Live has no line yet: its Rankings do not
/// hold final placings, and a history is what a Participant finished. The placing is the one of the ride's own Ranking, the one
/// of its Competition and Category; the other Rankings that count it are listed with the placing it has in each. A Ranking that
/// holds its final placings gives the rank it stores; one that does not is ranked in memory as the Results rank it, over the
/// Participations of its Event, which are read only then. A placing that cannot be told is left out and the ride is listed all
/// the same.
/// </summary>
internal sealed class ParticipationHistoryReader
{
    readonly CrossTenantReads _reads;
    readonly ILogger<ParticipationHistoryReader> _log;

    public ParticipationHistoryReader(CrossTenantReads reads, ILogger<ParticipationHistoryReader> log)
    {
        _reads = reads;
        _log = log;
    }

    /// <summary>The lines of the Athlete's or the Horse's history, the newest Event first.</summary>
    /// <param name="culture">The language the names of the other halves are told in.</param>
    /// <param name="now">The instant by which an Event has ended.</param>
    public async Task<IReadOnlyList<ParticipationHistory>> ReadAsync(
        Caller caller,
        ParticipantKind kind,
        Guid id,
        CultureInfo culture,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var rides =
            kind == ParticipantKind.Athlete
                ? await _reads.ReadRidesOfAthleteAsync(caller, id, cancellationToken)
                : await _reads.ReadRidesOfHorseAsync(caller, id, cancellationToken);
        var events = (
            await _reads.ReadStartedEventsAsync([.. rides.Select(x => x.EventId).Distinct()], cancellationToken)
        )
            .Where(x => EventStageRule.Of(true, x.EndDay, now) == EventStage.Historic)
            .ToDictionary(x => x.Id);
        var rankings = (await _reads.ReadRankingsOfEventsAsync([.. events.Keys], cancellationToken)).ToLookup(x =>
            x.EventId
        );
        var happenings = events.ToDictionary(x => x.Key, x => new HistoricEvent(x.Value, [.. rankings[x.Key]], _reads));

        var lines = new List<ParticipationHistory>();
        foreach (var ride in rides.Where(x => happenings.ContainsKey(x.EventId)))
        {
            lines.Add(await LineAsync(kind, ride, happenings[ride.EventId], culture, cancellationToken));
        }

        return [.. lines.OrderByDescending(x => x.EndDay).ThenByDescending(x => x.StartDay).ThenBy(x => x.Id)];
    }

    async Task<ParticipationHistory> LineAsync(
        ParticipantKind kind,
        ParticipationModel ride,
        HistoricEvent happening,
        CultureInfo culture,
        CancellationToken cancellationToken
    )
    {
        var participation = Entity(ride);

        // The first, by name, Category and id, of the Rankings that are the ride's own is the one.
        var counting = happening
            .Rankings.Where(x => x.Entries.Any(e => e.ParticipationId == ride.Id))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Category)
            .ThenBy(x => x.Id)
            .Select(x => new CountingRanking(x, Entity(x)))
            .ToList();
        var own =
            participation == null ? null : counting.FirstOrDefault(x => x.Ranking?.IsOwnOf(participation) == true);

        var also = new List<RankingPlacing>();
        foreach (var counted in counting.Where(x => x != own))
        {
            also.Add(
                new RankingPlacing(
                    counted.Model.Id,
                    counted.Model.Name,
                    counted.Model.Ruleset.ToString(),
                    counted.Model.Category.ToString(),
                    await PlacingAsync(counted.Ranking, participation, happening, cancellationToken)
                )
            );
        }

        var athlete = ride.Combination.Athlete;
        var horse = ride.Combination.Horse;
        var other =
            kind == ParticipantKind.Athlete
                ? new Participant(ParticipantKind.Horse, horse.Id, horse.Name, horse.NameEnglish)
                : new Participant(ParticipantKind.Athlete, athlete.Id, athlete.Name, athlete.NameEnglish);
        return new ParticipationHistory
        {
            Id = ride.Id,
            EventId = happening.Information.Id,
            EventName = happening.Information.Name,
            Location = happening.Information.Location,
            StartDay = happening.Information.StartDay,
            EndDay = happening.Information.EndDay,
            Competition = ride.Competition.Name,
            Ruleset = ride.Competition.Ruleset.ToString(),
            Category = ride.Category.ToString(),
            AthleteId = athlete.Id,
            HorseId = horse.Id,
            Other = other.NameIn(ride.Competition.Ruleset, culture),
            Placing = own == null ? null : await PlacingAsync(own.Ranking, participation, happening, cancellationToken),
            AlsoCountedIn = also,
        };
    }

    /// <summary>The ride as the domain knows it, or none when its document is not one: the line is told without a placing.</summary>
    Participation? Entity(ParticipationModel ride)
    {
        try
        {
            return ride.MapToEntity();
        }
        catch (Exception ex) when (ex is GuardException or ValidationException)
        {
            _log.LogWarning(
                ex,
                "Participation {ParticipationId} cannot be read and is listed without a placing.",
                ride.Id
            );
            return null;
        }
    }

    /// <summary>The Ranking as the domain knows it, or none when its document is not one: it is listed without a placing.</summary>
    Ranking? Entity(RankingModel model)
    {
        try
        {
            return model.MapToEntity();
        }
        catch (Exception ex) when (ex is GuardException or ValidationException)
        {
            _log.LogWarning(ex, "Ranking {RankingId} cannot be read and is listed without placings.", model.Id);
            return null;
        }
    }

    async Task<Placing?> PlacingAsync(
        Ranking? ranking,
        Participation? ride,
        HistoricEvent happening,
        CancellationToken cancellationToken
    )
    {
        if (ranking == null || ride == null)
        {
            return null;
        }

        try
        {
            var participations = ranking.IsFinal ? [] : await happening.ParticipationsAsync(cancellationToken);
            return ranking.PlacingOf(ride, participations, happening.Rules);
        }
        catch (Exception ex) when (ex is GuardException or ValidationException)
        {
            // a Ranking that counts a Participation that is not there has no placing to tell, as for the host
            _log.LogWarning(
                ex,
                "The placing of Participation {ParticipationId} in Ranking {RankingId} cannot be told and is left out.",
                ride.Id,
                ranking.Id
            );
            return null;
        }
    }

    /// <summary>A Ranking that counts a ride, as it is stored and as the domain knows it, when it does.</summary>
    sealed class CountingRanking
    {
        public CountingRanking(RankingModel model, Ranking? ranking)
        {
            Model = model;
            Ranking = ranking;
        }

        public RankingModel Model { get; }
        public Ranking? Ranking { get; }
    }

    /// <summary>An Event that has ended, the Rankings it keeps, and the Participations a Ranking is ranked over when it needs them.</summary>
    sealed class HistoricEvent
    {
        readonly CrossTenantReads _reads;
        IReadOnlyList<Participation>? _participations;

        public HistoricEvent(
            EventInformationModel information,
            IReadOnlyList<RankingModel> rankings,
            CrossTenantReads reads
        )
        {
            Information = information;
            Rankings = rankings;
            _reads = reads;
        }

        public EventInformationModel Information { get; }
        public IReadOnlyList<RankingModel> Rankings { get; }
        public RegionalRules? Rules => Information.RegionalRules?.MapToEntity();

        /// <summary>Every Participation of the Event, read the first time a Ranking is ranked over them.</summary>
        public async Task<IReadOnlyList<Participation>> ParticipationsAsync(CancellationToken cancellationToken)
        {
            if (_participations == null)
            {
                var models = await _reads.ReadParticipationsOfEventAsync(Information.Id, cancellationToken);
                _participations = [.. models.Select(x => x.MapToEntity())];
            }

            return _participations;
        }
    }
}
