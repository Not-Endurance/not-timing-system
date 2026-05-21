using System.Globalization;
using NTS.Domain.Helpers;

namespace NTS.Domain.Core.Aggregates;

public class Official : Aggregate, IEventScoped, INamed, INtsDisplayable
{
    public Official(
        string? name,
        string? nameEnglish,
        OfficialRole? role,
        int eventId,
        int? id = null,
        int? userId = null
    )
        : base(id)
    {
        EventId = eventId;
        Name = Required(nameof(Name), name);
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        Role = Required(nameof(Role), role);
        UserId = userId;
    }

    public int EventId { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
    public OfficialRole Role { get; }
    public int? UserId { get; }

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, ruleset, culture);
    }

    public string GetDisplayLabel(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return $"{GetDisplayName(ruleset, culture)}, {Role.GetDescription()}";
    }

    public override string ToString()
    {
        return GetDisplayLabel();
    }
}
