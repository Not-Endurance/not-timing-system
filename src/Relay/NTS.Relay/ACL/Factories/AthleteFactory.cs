using NTS.Relay.ACL.Entities.Athletes;
using NTS.Relay.ACL.Entities.Countries;
using NTS.Relay.ACL.Enums;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.ACL.Models;

namespace NTS.Relay.ACL.Factories;

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
            participation.Combination.Country?.IsoCode ?? "iso",
            participation.Combination.Country?.Name ?? "country-name",
            1337
        );
        return new EmsAthlete(athleteState, country);
    }
}
