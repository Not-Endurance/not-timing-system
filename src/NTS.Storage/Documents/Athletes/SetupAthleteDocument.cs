using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Documents.Clubs;
using NTS.Storage.Documents.Countries;

namespace NTS.Storage.Documents.Athletes;

public class SetupAthleteDocument : CoreAthleteModel, IDocument
{
    // TODO: if decide to use this approach integrate AutoMapper with specific mappings to solve duplicating mapping logic
    public static SetupAthleteDocument MapFrom(Athlete athlete)
    {
        return new SetupAthleteDocument
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Names = athlete.Names,
            Country = CountryDocument.Create(athlete.Country),
            Club = athlete.Club == null ? null : ClubDocument.Create(athlete.Club),
        };
    }

    public int Id { get; init; }

    public string TenantId { get; init; } = DEFAULT_TENANT;

    public Athlete MapToSetupAggregate()
    {
        return new Athlete(Id, Names, FeiId, Country?.ToDomain(), Club?.ToDomain());
    }
}
