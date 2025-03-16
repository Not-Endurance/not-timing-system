using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Athlete : IAthlete
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Athlete(int id, Person names, AthleteCategory category, Country country, Club? club, string feiId)
    {
        Id = id;
        Names = names;
        Category = category;
        Country = country;
        Club = club;
        FeiId = feiId;
    }

    public Athlete(IAthlete athlete)
    {
        Id = athlete.Id;
        Names = athlete.Names;
        Category = athlete.Category;
        Country = athlete.Country;
        FeiId = athlete.FeiId;
    }

    public int Id { get; }
    public Person Names { get; }
    public AthleteCategory Category { get; }
    public Country Country { get; }
    public IClub? Club { get; }
    public string? FeiId { get; }

    public override string ToString()
    {
        return $"{Category}: {Names}";
    }
}
