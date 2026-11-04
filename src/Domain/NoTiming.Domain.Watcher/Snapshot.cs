using System.Globalization;
using NC.Structures;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Helpers;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Watcher;

public record Snapshot : IIdentifiable, INamed, INoTimingDisplayable
{
    public Snapshot(
        int number,
        string? name,
        string? nameEnglish,
        Timestamp? timestamp = null,
        CompetitionRuleset? ruleset = null
    )
    {
        Number = number;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Snapshot name is required.", nameof(name))
            : name;
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        Timestamp = timestamp;
        Ruleset = ruleset;
    }

    public int Number { get; }
    public int Id => Number;
    public string Name { get; }
    public string? NameEnglish { get; }
    public CompetitionRuleset? Ruleset { get; }
    public Timestamp? Timestamp { get; set; }

    public string GetDisplayName(CompetitionRuleset? ruleset = null, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, ruleset ?? Ruleset, culture);
    }

    public override string ToString()
    {
        return $"#{Number}: {GetDisplayName()}";
    }
}
