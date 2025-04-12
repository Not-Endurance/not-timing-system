using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities.Athletes;
using NTS.Warp.ACL.Entities.Countries;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Models;

namespace NTS.Warp.ACL.Factories;

public class AthleteFactory
{
    public static EmsAthlete Create(Participation participation)
    {
        var athleteState = new EmsAthleteState
        {
            Category = EmsCategory.Seniors, //TODO: after athlete
            Club = participation.Combination.Club?.ToString(),
            FeiId = "", //TODO: after athlete
            FirstName = participation.Combination.Athlete.ToString().Split().First(),
            LastName = participation.Combination.Athlete.ToString().Split().Last(),
            Id = participation.Combination.Id,
        };
        var country = new EmsCountry(
            participation.Combination.Athlete.Country?.IsoCode ?? "iso",
            participation.Combination.Athlete.Country?.Name ?? "country-name",
            1337
        );
        return new EmsAthlete(athleteState, country);
    }
}
