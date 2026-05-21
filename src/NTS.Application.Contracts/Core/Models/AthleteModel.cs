using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Application.Contracts.Core.Models;

public class AthleteModel
{
    public static AthleteModel MapFrom(Athlete athlete)
    {
        return new AthleteModel
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Name = athlete.Name,
            NameEnglish = athlete.NameEnglish,
            Country = CountryModel.From(athlete.Country),
            Club = athlete.Club == null ? null : ClubModel.MapFrom(athlete.Club),
        };
    }

    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string? NameEnglish { get; init; }
    public CountryModel Country { get; init; } = default!;
    public ClubModel? Club { get; init; }
    public string? FeiId { get; init; }

    public Athlete MapToEntity()
    {
        var country = Country.MapToEntity();
        var club = Club?.MapToEntity();
        return new Athlete(Name, NameEnglish, country, club, FeiId, Id);
    }
}
