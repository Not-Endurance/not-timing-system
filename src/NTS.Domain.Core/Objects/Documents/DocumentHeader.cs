using Not.Domain.Base;
using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Documents;

public record DocumentHeader : DomainObject
{
    public DocumentHeader(
        string enduranceEvent,
        PopulatedPlace populatedPlace,
        EventSpan eventSpan,
        IEnumerable<Official> officials
    )
    {
        Title = enduranceEvent;
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        GroundJuryPresident = officials.FirstOrDefault(x => x.Role == OfficialRole.GroundJuryPresident);
        VeterinaryCommissionPresident = officials.FirstOrDefault(x =>
            x.Role == OfficialRole.VeterinaryCommissionPresident
        );
        TechnicalDelegate = officials.FirstOrDefault(x => x.Role == OfficialRole.TechnicalDelegate);
        ForeignVeterinaryDelegate = officials.FirstOrDefault(x => x.Role == OfficialRole.ForeignVeterinaryDelegate);
        ForeignJudge = officials.FirstOrDefault(x => x.Role == OfficialRole.ForeignJudge);
        ChiefSteward = officials.FirstOrDefault(x => x.Role == OfficialRole.ChiefSteward);
    }

    public string Title { get; }
    public PopulatedPlace PopulatedPlace { get; }
    public EventSpan EventSpan { get; }
    public Official? GroundJuryPresident { get; }
    public Official? VeterinaryCommissionPresident { get; }
    public Official? TechnicalDelegate { get; }
    public Official? ForeignVeterinaryDelegate { get; }
    public Official? ForeignJudge { get; }
    public Official? ChiefSteward { get; }
}
