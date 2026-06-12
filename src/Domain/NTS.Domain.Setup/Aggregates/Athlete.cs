using System.Globalization;
using Not.Domain.Exceptions;
using Not.Domain.Krud;
using NTS.Domain.Aggregates;
using NTS.Domain.Helpers;

namespace NTS.Domain.Setup.Aggregates;

public class Athlete : Aggregate, IKurdMirror<Club>, INamed, INtsDisplayable
{
    public Athlete(
        string? name,
        string? nameEnglish,
        string? feiId,
        Country? country,
        Club? club,
        int? id = null,
        User? user = null
    )
        : base(id)
    {
        FeiId = ValidateFeiId(feiId);
        Name = Required(nameof(Name), name);
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        Country = Required(nameof(Country), country);
        Club = club;
        User = user;
    }

    public string? FeiId { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
    public Country Country { get; }
    public Club? Club { get; private set; }
    public User? User { get; }

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, ruleset, culture);
    }

    public override string ToString()
    {
        return GetDisplayName();
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
