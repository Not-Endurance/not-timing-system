using System.Globalization;
using NoTiming.Domain.Helpers;

namespace NoTiming.Domain.Setup.Aggregates;

public class Horse : Aggregate, INamed, INoTimingDisplayable
{
    public Horse(string? name, string? nameEnglish, string? feiId, int? id = null)
        : base(id)
    {
        Name = Required(nameof(Name), name);
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        FeiId = feiId;
    }

    public string Name { get; }
    public string? NameEnglish { get; }
    public string? FeiId { get; }

    public string GetDisplayName(bool englishNames = false, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, englishNames, culture);
    }

    public string Summarize()
    {
        return ToString();
    }

    public override string ToString()
    {
        return GetDisplayName();
    }
}
