using NoTiming.Domain.Core.Objects;

namespace NoTiming.Domain.Core.Tests.Objects;

public sealed class EventSpanTests
{
    const string SOFIA = "Europe/Sofia";

    [Fact]
    public void Constructor_WhenEventIsInSummerTime_ShouldEndAtLocalEndOfDay()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(3)),
            SOFIA
        );

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 23, 59, 59, TimeSpan.FromHours(3)), span.EndDay);
    }

    [Fact]
    public void Constructor_WhenEventIsInWinterTime_ShouldEndAtLocalEndOfDay()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.FromHours(2)),
            SOFIA
        );

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 23, 59, 59, TimeSpan.FromHours(2)), span.EndDay);
    }

    [Fact]
    public void Constructor_WhenEndDayIsInAnotherOffset_ShouldUseTheEventZoneDate()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 7, 1, 23, 30, 0, TimeSpan.FromHours(-5)),
            SOFIA
        );

        Assert.Equal(new DateTimeOffset(2026, 7, 2, 23, 59, 59, TimeSpan.FromHours(3)), span.EndDay);
    }

    [Fact]
    public void Constructor_WhenStartDayHasATime_ShouldStartAtLocalMidnight()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 7, 1, 15, 30, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 7, 1, 17, 0, 0, TimeSpan.FromHours(3)),
            SOFIA
        );

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(3)), span.StartDay);
    }

    [Fact]
    public void Constructor_WhenStoredInstantsAreAlreadyInTheZone_ShouldKeepThem()
    {
        var first = new EventSpan(
            new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.FromHours(3)),
            SOFIA
        );

        var reloaded = new EventSpan(first.StartDay, first.EndDay, SOFIA);

        Assert.Equal(first.StartDay, reloaded.StartDay);
        Assert.Equal(first.EndDay, reloaded.EndDay);
    }

    [Fact]
    public void Constructor_WhenTimeZoneIsGiven_ShouldKeepTheTimeZoneId()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(3)),
            SOFIA
        );

        Assert.Equal(SOFIA, span.TimeZoneId);
    }
}
