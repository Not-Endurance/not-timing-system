using NC.Krud.Models;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.StaticOptions;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Judge.Contracts.Features.Setup.Athletes;

public record AthleteFormModel : KrudFormModel<Athlete>
{
    public AthleteFormModel()
    {
#if DEBUG
        Name = "Gucci Petrov";
        NameEnglish = "Gucci Petrov";
        Club = new("Конярче ЕООД");
#endif
        Country = StaticOption.SelectedCountry;
    }

    public AthleteFormModel(KrudFormModel<Athlete> original)
        : base(original) { }

    public string? Name { get; set; }
    public string? NameEnglish { get; set; }
    public string? FeiId { get; set; }
    public Country? Country { get; set; }
    public Club? Club { get; set; }
    public User? User { get; set; }

    protected override Athlete MapTo()
    {
        return new Athlete(Name, NameEnglish, FeiId, Country, Club, Id, User);
    }

    public override void MapFrom(Athlete athlete)
    {
        Id = athlete.Id;
        Name = athlete.Name;
        NameEnglish = athlete.NameEnglish;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club;
        User = athlete.User;
    }
}
