using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Storage.Documents.Clubs;
using NTS.Storage.Documents.Countries;

namespace NTS.Storage.Documents.Athletes;

public class CoreAthleteModel
{
    public static CoreAthleteModel MapFrom(Athlete athlete)
    {
        return new CoreAthleteModel
        {
            FeiId = athlete.FeiId,
            Names = athlete.Names,
            Country = CountryDocument.Create(athlete.Country),
            Club = athlete.Club == null ? null : ClubDocument.Create(athlete.Club),
        };
    }

    public string[] Names { get; init; } = default!;
    public CountryDocument? Country { get; init; } // TODO: should be required
    public ClubDocument? Club { get; init; }
    public string? FeiId { get; init; }

    public IAthlete MapToAthlete()
    {
        return new Athlete(
            Names, 
            Country!.ToDomain(),
            Club == null ? null : new Club(Club.Id, Club.Name), 
            FeiId!);
    }
}
