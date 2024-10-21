using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects;

public record DocumentHeader : DomainObject
{
    public DocumentHeader(string enduranceEvent, PopulatedPlace populatedPlace, EventSpan eventSpan, IEnumerable<Official> officials)
    {
        Title = enduranceEvent;
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        GroundJuryPresident = officials.FirstOrDefault(x => x.Role == OfficialRole.GroundJuryPresident);
        VeterinaryCommissionPresident = officials.FirstOrDefault(x => x.Role == OfficialRole.VeterinaryCommissionPresident);
        TechnicalDelegate = officials.FirstOrDefault(x => x.Role == OfficialRole.TechnicalDelegate);
        ForeignVeterinaryDelegate = officials.FirstOrDefault(x => x.Role == OfficialRole.ForeignVeterinaryDelegate);
        ForeignJudge = officials.FirstOrDefault(x => x.Role == OfficialRole.ForeignJudge);
        ChiefSteward = officials.FirstOrDefault(x => x.Role == OfficialRole.ChiefSteward);
    }

    public string Title { get; private set; }
    public PopulatedPlace PopulatedPlace { get; private set; }
    public EventSpan EventSpan { get; private set; }
    public Official? GroundJuryPresident { get; private set; }
    public Official? VeterinaryCommissionPresident { get;  private set; }
    public Official? TechnicalDelegate { get; private set; }
    public Official? ForeignVeterinaryDelegate { get; private set; }
    public Official? ForeignJudge { get; private set; }
    public Official? ChiefSteward { get; private set; }
}
