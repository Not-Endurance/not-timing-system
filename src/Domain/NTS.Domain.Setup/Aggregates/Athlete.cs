using Not.Domain.Exceptions;
using Not.Domain.Krud;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : Aggregate, IKurdMirror<Club>
{
    public Athlete(Person? names, string? feiId, Country? country, Club? club, int? id = null, User? user = null)
        : base(id)
    {
        FeiId = ValidateFeiId(feiId);
        Names = Required(nameof(Names), names);
        Country = Required(nameof(Country), country);
        Club = club;
        User = user;
    }

    public string? FeiId { get; }
    public Person Names { get; }
    public Country Country { get; }
    public Club? Club { get; private set; }
    public User? User { get; }

    public override string ToString()
    {
        return Names.ToString();
    }

    public bool Reflect(Club club)
    {
        if (Club != club)
        {
            return false;
        }
        Club = club;
        return true;
    }

    string? ValidateFeiId(string? feiId)
    {
        if (feiId == null)
        {
            return null;
        }
        if (!int.TryParse(feiId, out var _))
        {
            throw new DomainPropertyException(nameof(FeiId), Athlete_FEI_ID_must_be_numeric_value_string);
        }
        return feiId;
    }
}
