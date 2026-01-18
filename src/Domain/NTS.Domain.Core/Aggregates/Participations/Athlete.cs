using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Athlete : Entity, IAthlete
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Athlete(Person names, Country country, Club? club, string? feiId)
        : base(names, country)
    {
        Names = names;
        Country = country;
        Club = club;
        FeiId = feiId;
    }

    // TODO: Probably move this applicaiton layer
    public Athlete(IAthlete athlete)
        : this(athlete.Names, athlete.Country, athlete.Club as Club, athlete.FeiId) { }

    IClub? IAthlete.Club => Club;
    public Person Names { get; }
    public Country Country { get; }
    public Club? Club { get; }
    public string? FeiId { get; }

    public override string ToString()
    {
        return $"{Names}, {Country}";
    }
}
