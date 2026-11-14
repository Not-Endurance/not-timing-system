using System.Globalization;
using NoTiming.Domain.Helpers;

namespace NoTiming.Domain.Core.Aggregates.Participations.Entities;

public class Horse : Entity, INamed, INoTimingDisplayable
{
    public Horse(string? name, string? nameEnglish, string? feiId, int id)
        : base(id)
    {
        Name = Required(nameof(Name), name);
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        FeiId = feiId;
    }

    public string Name { get; private set; }
    public string? NameEnglish { get; private set; }
    public string? FeiId { get; private set; }

    public string GetDisplayName(bool englishNames = false, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, englishNames, culture);
    }

    public override string ToString()
    {
        return GetDisplayName();
    }
}
