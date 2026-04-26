using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;


namespace NTS.Application.Contracts.Core.Models;

public class RanklistModel
{
    public static RanklistModel MapFrom(Ranklist ranklist)
    {
        return new RanklistModel
        {
            Id = ranklist.RankingId,
            Name = ranklist.Name,
            Ruleset = ranklist.Ruleset,
            Type = ranklist.Type,
            Category = ranklist.Category,
            CompetitionFeiId = ranklist.Ranking.CompetitionFeiId,
            FeiRule = ranklist.Ranking.FeiRule,
            FeiScheduleNumber = ranklist.Ranking.FeiScheduleNumber,
            Entries = ranklist.Entries.Select(RankingEntryModel.MapFrom).ToArray(),
        };
    }

    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public ParticipationCategory Category { get; init; }
    public string? CompetitionFeiId { get; init; }
    public string? FeiRule { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public RankingEntryModel[] Entries { get; init; } = [];

    public Ranklist MapToEntity()
    {
        var entries = Entries.Select(x => x.MapToEntity()).ToList();
        var competition = new Competition(Name, Ruleset, Type);
        var eventId = Entries.FirstOrDefault()?.Participation.EventId ?? Id;
        var ranking = new Ranking(
            Name,
            Ruleset,
            Type,
            Category,
            CompetitionFeiId,
            FeiRule,
            FeiScheduleNumber,
            entries,
            eventId,
            Id
        );
        return new Ranklist(ranking, entries);
    }
}


