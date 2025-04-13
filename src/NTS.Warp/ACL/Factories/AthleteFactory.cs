using NTS.Domain.Enums;
using NTS.Warp.ACL.Entities.Athletes;
using NTS.Warp.ACL.Entities.Countries;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Models;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class AthleteFactory
{
    public static EmsAthlete Create(ParticipationWarpDto.AthleteDto athlete)
    {
        var athleteState = new EmsAthleteState
        {
            Category = EmsCategory.Seniors, //TODO: after athlete
            Club = athlete.Club,
            FeiId = athlete.FeiId, //TODO: after athlete
            FirstName = athlete.Person.GetFirstName(),
            LastName = athlete.Person.GetLastName(),
            Id = athlete.Id,
        };
        var country = new EmsCountry(
            athlete.Country?.IsoCode ?? "iso",
            athlete.Country?.Name ?? "country-name",
            1337
        );
        return new EmsAthlete(athleteState, country);
    }

    public EmsCategory MapCategory(AthleteCategory category)
    {
        return category switch
        {
            AthleteCategory.Senior => EmsCategory.Seniors,
            AthleteCategory.Children => EmsCategory.Children,
            AthleteCategory.JuniorOrYoungAdult => EmsCategory.JuniorOrYoungAdults,
            AthleteCategory.Training
                or AthleteCategory.Companion => EmsCategory.Seniors,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };
    }
}
