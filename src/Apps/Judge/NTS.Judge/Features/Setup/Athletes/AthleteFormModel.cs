using Not.Krud.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.StaticOptions;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public record AthleteFormModel : KrudFormModel<Athlete>
{
    public AthleteFormModel()
    {
#if DEBUG
        Names = "Gucci Petrov";
        Club = new("Конярче ЕООД");
#endif
        Country = StaticOption.SelectedCountry;
    }

    public AthleteFormModel(KrudFormModel<Athlete> original)
        : base(original) { }

    public string? Names { get; set; }
    public string? FeiId { get; set; }
    public Country? Country { get; set; }
    public Club? Club { get; set; }
    public User? User { get; set; }

    protected override Athlete MapTo()
    {
        return new Athlete(Person.Create(Names), FeiId, Country, Club, Id, User);
    }

    public override void MapFrom(Athlete athlete)
    {
        Id = athlete.Id;
        Names = athlete.Names;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club;
        User = athlete.User;
    }
}
