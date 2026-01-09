using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.StaticOptions;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public class AthleteFormModel : IFormModel<Athlete>
{
    public AthleteFormModel()
    {
#if DEBUG
        Name = "Gucci Petrov";
        Club = Club.Create("Конярче ЕООД");
#endif
        Country = StaticOption.SelectedCountry;
    }

    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? FeiId { get; set; }
    public Country? Country { get; set; }
    public Club? Club { get; set; }

    public void FromEntity(Athlete athlete)
    {
        Id = athlete.Id;
        Name = athlete.Names;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club;
    }
}
