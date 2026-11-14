using System.Globalization;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Helpers;

namespace NoTiming.Domain.Core.Aggregates.Participations.Entities;

public class Athlete : Entity, INamed, INoTimingDisplayable
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

    public string GetDisplayName(bool englishNames = false, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, englishNames, culture);
    }

    public override string ToString()
    {
        return $"{GetDisplayName()}, {Country}";
    }
}
