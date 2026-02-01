using Not.Krud.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.StaticOptions;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public class AthleteFormModel : KrudFormModel<Athlete>
{
    public AthleteFormModel()
    {
#if DEBUG
        Name = "Gucci Petrov";
        Club = new("Конярче ЕООД");
#endif
        Country = StaticOption.SelectedCountry;
    }

    public string? Name { get; set; }
    public string? FeiId { get; set; }
    public Country? Country { get; set; }
    public Club? Club { get; set; }

    protected override Athlete MapTo()
    {
        return new Athlete(Person.Create(Name), FeiId, Country, Club, Id);
    }

    public override void MapFrom(Athlete athlete)
    {
        Id = athlete.Id;
        Name = athlete.Names;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club;
    }
}
