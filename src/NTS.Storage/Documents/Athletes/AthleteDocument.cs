using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Documents.Clubs;
using NTS.Storage.Documents.Countries;

namespace NTS.Storage.Documents.Athletes;

public class AthleteDocument : Document
{
    public static AthleteDocument Create(IAthlete athlete)
    {
        return new AthleteDocument
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Names = athlete.Names,
            Category = athlete.Category,
            Country = CountryDocument.Create(athlete.Country),
            Club = athlete.Club == null ? null : ClubDocument.Create(athlete.Club),
        };
    }

    public string[] Names { get; init; } = default!;
    public AthleteCategory Category { get; init; } = default!;
    public CountryDocument? Country { get; init; } // TODO: should be required
    public ClubDocument? Club { get; init; }
    public string? FeiId { get; init; }

    public Athlete ToDomain()
    {
        return new Athlete(Id, Names, FeiId, Country?.ToDomain(), Club?.ToDomain(), Category);
    }
}
