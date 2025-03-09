using Not.Random;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Documents.Clubs;
using NTS.Storage.Documents.Countries;

namespace NTS.Storage.Documents.Athletes;

public class AthleteDocument : Document
{
    public AthleteDocument(IAthlete athlete)
        : base(athlete.Id)
    {
        FeiId = athlete.FeiId;
        Names = athlete.Names;
        Category = athlete.Category;
        Country = new CountryDocument(athlete.Country);
        Club = athlete.Club == null ? null : new ClubDocument(athlete.Club);
    }

    public string? FeiId { get; init; }
    public string[] Names { get; init; }
    public AthleteCategory Category { get; init; }
    public CountryDocument? Country { get; init; } // TODO: should be required
    public ClubDocument? Club { get; init; }

    public Athlete ToDomain()
    {
        return new Athlete(Id, Names, FeiId, Country?.ToDomain(), Club?.ToDomain(), Category);
    }
}
