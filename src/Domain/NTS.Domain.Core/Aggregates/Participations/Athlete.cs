using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Athlete : Entity
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Athlete(Person names, Country country, Club? club, string? feiId) : base(names, country)
    {
        Names = names;
        Country = country;
        Club = club;
        FeiId = feiId;
    }

    public Person Names { get; }
    public Country Country { get; }
    public Club? Club { get; }
    public string? FeiId { get; }

    public override string ToString()
    {
        return $"{Names}, {Country}";
    }
}
