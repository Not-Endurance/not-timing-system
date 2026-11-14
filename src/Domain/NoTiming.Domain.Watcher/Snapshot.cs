using System.Globalization;
using NC.Structures;
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
        bool englishNames = false
    )
    {
        Number = number;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Snapshot name is required.", nameof(name))
            : name;
        NameEnglish = string.IsNullOrWhiteSpace(nameEnglish) ? null : nameEnglish;
        Timestamp = timestamp;
        EnglishNames = englishNames;
    }

    public int Number { get; }
    public int Id => Number;
    public string Name { get; }
    public string? NameEnglish { get; }
    public bool EnglishNames { get; }
    public Timestamp? Timestamp { get; set; }

    public string GetDisplayName(bool englishNames = false, CultureInfo? culture = null)
    {
        return NameRenderingHelper.Render(Name, NameEnglish, englishNames || EnglishNames, culture);
    }

    public override string ToString()
    {
        return $"#{Number}: {GetDisplayName()}";
    }
}
