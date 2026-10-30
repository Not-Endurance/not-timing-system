using NoTiming.Ui.Features.Core.Snapshots;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>The time a person types to correct the entry of the History (#618): a time of the day on the day of the entry, as an instant.</summary>
public sealed class CorrectedTimeTests
{
    static readonly TimeZoneInfo SOFIA = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");

    [Fact]
    public void The_time_of_the_day_is_taken_on_the_day_of_the_entry_and_not_on_the_day_the_page_is_open()
    {
        var entry = new Timestamp(new DateTimeOffset(2026, 10, 11, 23, 59, 30, TimeSpan.FromHours(3)));

        var corrected = CorrectedTime.Of(entry, new TimeSpan(23, 50, 0), SOFIA);

        Assert.Equal(new DateTime(2026, 10, 11, 20, 50, 0, DateTimeKind.Utc), corrected.ToDateTimeOffset().UtcDateTime);
    }

    [Fact]
    public void The_offset_is_the_one_the_zone_has_on_the_wall_time_that_was_typed_and_not_the_one_of_the_entry()
    {
        var beforeTheChange = new Timestamp(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.FromHours(2)));

        var afterTheChange = CorrectedTime.Of(beforeTheChange, new TimeSpan(4, 0, 0), SOFIA); // the clocks went forward at three

        Assert.Equal(
            new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc),
            afterTheChange.ToDateTimeOffset().UtcDateTime
        );
    }

    [Fact]
    public void The_day_is_the_one_the_entry_has_in_the_zone_of_the_person_whatever_the_zone_of_the_machine()
    {
        var entry = new Timestamp(new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        var east = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati"); // it is the 11th there, and
        var west = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Pago_Pago"); // the 10th here

        var inTheEast = CorrectedTime.Of(entry, new TimeSpan(10, 0, 0), east);
        var inTheWest = CorrectedTime.Of(entry, new TimeSpan(10, 0, 0), west);

        Assert.Equal(new DateTime(2026, 6, 10, 20, 0, 0, DateTimeKind.Utc), inTheEast.ToDateTimeOffset().UtcDateTime);
        Assert.Equal(new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc), inTheWest.ToDateTimeOffset().UtcDateTime);
    }

    [Fact]
    public void A_time_of_the_day_that_is_the_entrys_own_is_its_instant()
    {
        var entry = new Timestamp(new DateTimeOffset(2026, 10, 11, 10, 15, 30, TimeSpan.FromHours(3)));

        var same = CorrectedTime.Of(entry, new TimeSpan(10, 15, 30), SOFIA);

        Assert.Equal(entry.ToDateTimeOffset(), same.ToDateTimeOffset());
    }
}
