using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Core.Snapshots;

/// <summary>
/// The time a person types to correct the time of an entry of the History (#618): a time of the day, which is taken on the
/// day of the entry in the zone of the person (and not on the day the page is open) and with the offset the zone has on that wall
/// time, as an Update names an instant.
/// </summary>
public static class CorrectedTime
{
    public static Timestamp Of(Timestamp entry, TimeSpan timeOfDay, TimeZoneInfo zone)
    {
        var wall = TimeZoneInfo.ConvertTime(entry.ToDateTimeOffset(), zone).Date + timeOfDay;
        return new Timestamp(new DateTimeOffset(wall, zone.GetUtcOffset(wall)));
    }
}
