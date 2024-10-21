using NTS.Domain.Objects;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Athletes;
public class AthleteFormModel
{
    public AthleteFormModel()
    {
        // mock data for testing
        Name = "Gucci Petrov";
        Club = "Конярче ООД";
        Country = new Country("BG", "zz", "Bulgaria");
    }
    public AthleteFormModel(Athlete athlete)
    {
        Id = athlete.Id;
        Name = athlete.Person;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club.ToString();
        Category = athlete.Category;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string? FeiId { get; set; }
    public Country Country { get; set; }
    public string Club { get; set; }
    public AthleteCategory Category { get; set; } = AthleteCategory.Senior;

}
