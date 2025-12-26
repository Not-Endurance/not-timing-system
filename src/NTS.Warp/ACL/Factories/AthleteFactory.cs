using NTS.Domain.Enums;
using NTS.Warp.ACL.Entities.Athletes;
using NTS.Warp.ACL.Entities.Countries;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Models;
using NTS.Warp.Features.Judge.Models;


namespace NTS.Warp.ACL.Factories;

public class AthleteFactory
{
    public static EmsAthlete Create(AthleteModel athlete)
    {
        var athleteState = new EmsAthleteState
        {
            Category = EmsCategory.Seniors, //TODO: after athlete
            Club = athlete.Club.Name,
            FeiId = athlete.FeiId, //TODO: after athlete
            FirstName = athlete.Names.ElementAtOrDefault(0),
            LastName = athlete.Names.ElementAtOrDefault(1),
            Id = athlete.Id,
        };
        var country = new EmsCountry(athlete.Country?.IsoCode ?? "iso", athlete.Country?.Name ?? "country-name", 1337);
        return new EmsAthlete(athleteState, country);
    }

    public EmsCategory MapCategory(ParticipationCategory category)
    {
        return category switch
        {
            ParticipationCategory.Senior => EmsCategory.Seniors,
            ParticipationCategory.Children => EmsCategory.Children,
            ParticipationCategory.JuniorOrYoungAdult => EmsCategory.JuniorOrYoungAdults,
            ParticipationCategory.Training or ParticipationCategory.Companion => EmsCategory.Seniors,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }
}
