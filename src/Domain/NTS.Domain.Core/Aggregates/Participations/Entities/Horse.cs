using System.Globalization;
using NTS.Domain.Helpers;

namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Horse : Entity, INamed, INtsDisplayable
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

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, ruleset, culture);
    }

    public override string ToString()
    {
        return GetDisplayName();
    }
}
