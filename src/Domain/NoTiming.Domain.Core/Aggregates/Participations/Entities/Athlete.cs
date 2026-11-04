using System.Globalization;
using NTS.Domain.Aggregates;
using NTS.Domain.Helpers;

namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Athlete : Entity, INamed, INtsDisplayable
{
    public Athlete(string? name, string? nameEnglish, Country country, Club? club, string? feiId, int id)
        : base(id)
    {
        Name = Required(nameof(Name), name);
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        Country = country;
        Club = club;
        FeiId = feiId;
    }

    public string Name { get; }
    public string? NameEnglish { get; }
    public Country Country { get; }
    public Club? Club { get; }
    public string? FeiId { get; }

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, ruleset, culture);
    }

    public override string ToString()
    {
        return $"{GetDisplayName()}, {Country}";
    }
}
