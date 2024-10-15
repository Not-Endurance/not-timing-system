using Not.Blazor.Forms;
using Not.Blazor.Ports;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Athletes;

public class AthleteFormModel : IFormModel<Athlete>
{
    public static AthleteFormModel FromEntiy(Athlete athlete)
    {
        var model = new AthleteFormModel();
        model.FromEntity(athlete);
        return model;
    }

    public AthleteFormModel()
    {
        // mock data for testing
        Name = "Gucci Petrov";
        Club = "Конярче ООД";
        Category = "Олимпийски надежди";
        Country = new Country("BG", "zz", "Bulgaria");
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string? FeiId { get; set; }
    public Country Country { get; set; }
    public string Club { get; set; }
    public string Category { get; set; }

    public void FromEntity(Athlete athlete)
    {
        Id = athlete.Id;
        Name = athlete.Person;
        FeiId = athlete.FeiId;
        Country = athlete.Country;
        Club = athlete.Club.ToString();
        Category = athlete.Category;
    }
}