using MongoDB.Bson;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;

namespace NTS.Application.Factories;

/// <summary>
/// What starting an Event makes from its Setup (ADR-0006, #628): the Core document of the Event, and beside it the copies of
/// its Officials and Operators and the Participations and Rankings of its competitions, each a document with the id of the
/// Event. It is the one place that says what an Event is made of, so that the Api that starts an Event and the command that
/// seeds one for staging make the same documents (#607). The Setup is taken to be whole: the validation that refuses one
/// that is not belongs to who asks.
/// </summary>
public sealed class EventStartDocuments
{
    /// <summary>The documents the Event is made of. The rules of the Regional competitions are those of its Tenant now (ADR-0012).</summary>
    public static EventStartDocuments From(ConfigureEvent setup, RegionalRules rules)
    {
        var eventInformation = EventInformationModel.From(EventInformationFactory.Create(setup, rules));
        var (participations, rankings) = CreateParticipationsAndRankings(setup);
        return new EventStartDocuments(
            eventInformation,
            [
                .. setup.Officials.Select(x =>
                    OfficialModel.MapFrom(OfficialFactory.Create(x, setup.Id)).ToBsonDocument()
                ),
            ],
            [
                .. setup.Operators.Select(x =>
                    OperatorModel.MapFrom(OperatorFactory.Create(x, setup.Id)).ToBsonDocument()
                ),
            ],
            [.. participations.Select(x => ParticipationModel.MapFrom(x).ToBsonDocument())],
            [.. rankings.Select(x => RankingModel.From(x).ToBsonDocument())]
        );
    }

    EventStartDocuments(
        EventInformationModel eventInformation,
        IReadOnlyList<BsonDocument> officials,
        IReadOnlyList<BsonDocument> operators,
        IReadOnlyList<BsonDocument> participations,
        IReadOnlyList<BsonDocument> rankings
    )
    {
        EventInformation = eventInformation;
        Officials = officials;
        Operators = operators;
        Participations = participations;
        Rankings = rankings;
    }

    public EventInformationModel EventInformation { get; }
    public IReadOnlyList<BsonDocument> Officials { get; }
    public IReadOnlyList<BsonDocument> Operators { get; }
    public IReadOnlyList<BsonDocument> Participations { get; }
    public IReadOnlyList<BsonDocument> Rankings { get; }

    static (
        IReadOnlyList<Participation> Participations,
        IReadOnlyList<Ranking> Rankings
    ) CreateParticipationsAndRankings(ConfigureEvent setup)
    {
        var participations = new List<Participation>();
        var rankings = new List<Ranking>();
        foreach (var competition in setup.Competitions)
        {
            var (made, entriesByCategory) = ParticipationAndRankingFactory.Create(
                competition,
                participations,
                setup.Id
            );
            participations.AddRange(made);
            rankings.AddRange(entriesByCategory.Select(x => CreateRanking(competition, x, setup.Id)));
        }

        return (participations, rankings);
    }

    static Ranking CreateRanking(
        SetupCompetition competition,
        KeyValuePair<ParticipationCategory, List<RankingEntry>> entriesByCategory,
        Guid eventId
    )
    {
        return new Ranking(
            competition.Name,
            competition.Ruleset,
            entriesByCategory.Key,
            competition.FeiEventId,
            competition.FeiEventCode,
            competition.FeiCompetitionId,
            competition.FeiRule,
            competition.FeiScheduleNumber,
            entriesByCategory.Value,
            eventId
        );
    }
}
