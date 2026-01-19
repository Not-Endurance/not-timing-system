using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Athlete : Entity
{
    public Athlete(int id, Person names, Country country, Club? club, string? feiId) : base(id)
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
