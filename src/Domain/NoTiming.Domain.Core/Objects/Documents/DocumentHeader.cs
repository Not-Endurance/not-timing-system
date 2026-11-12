using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Documents;

public record DocumentHeader
{
    public DocumentHeader(
        string eventInformation,
        bool englishNames,
        Country country,
        string location,
        EventSpan eventSpan,
        IEnumerable<Official> officials
    )
    {
        Title = eventInformation;
        EnglishNames = englishNames;
        Country = country;
        Location = location;
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
    public bool EnglishNames { get; }
    public Country Country { get; }
    public string Location { get; }
    public EventSpan EventSpan { get; }
    public Official? GroundJuryPresident { get; }
    public Official? VeterinaryCommissionPresident { get; }
    public Official? TechnicalDelegate { get; }
    public Official? ForeignVeterinaryDelegate { get; }
    public Official? ForeignJudge { get; }
    public Official? ChiefSteward { get; }
}
