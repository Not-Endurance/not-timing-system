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

public class AthleteModel
{
    public static AthleteModel MapFrom(Athlete athlete)
    {
        return new AthleteModel
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Names = athlete.Names.Names,
            Country = CountryModel.From(athlete.Country),
            Club = athlete.Club == null ? null : ClubModel.MapFrom(athlete.Club),
        };
    }

    public int Id { get; init; }
    public string[] Names { get; init; } = default!;
    public CountryModel Country { get; init; } = default!;
    public ClubModel? Club { get; init; }
    public string? FeiId { get; init; }

    public Athlete MapToEntity()
    {
        var country = Country.MapToEntity();
        var club = Club?.MapToEntity();
        return new Athlete(Names, country, club, FeiId, Id);
    }
}


