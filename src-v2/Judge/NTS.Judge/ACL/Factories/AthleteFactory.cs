using NTS.Compatibility.EMS.Entities.Athletes;
using NTS.Compatibility.EMS.Entities.Countries;
using NTS.Compatibility.EMS.Enums;
using NTS.Domain.Core.Entities;
using NTS.Judge.ACL.Bridge;

namespace NTS.Judge.ACL.Factories;

public class AthleteFactory
{
    public static EmsAthlete Create(Participation participation)
    {
        var athleteState = new EmsAthleteState
        {
            Category = EmsCategory.Seniors, //TODO: after athlete
            Club = participation.Tandem.Club?.ToString(),
            FeiId = "", //TODO: after athlete
            FirstName = participation.Tandem.Name.ToString().Split().First(),
            LastName = participation.Tandem.Name.ToString().Split().Last(),
            Id = participation.Tandem.Id,
        };
        var country = new EmsCountry(participation.Tandem.Country?.IsoCode ?? "iso", participation.Tandem.Country?.Name ?? "country-name", 1337);
        return new EmsAthlete(athleteState, country);
    }
}
