using NTS.Domain.Core.Objects;

namespace NTS.Judge.Tests.Core;

public class EventSpanTests
{
    [Fact]
    public void IsActive_WhenEventStartsInFuture_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        var span = new EventSpan(
            new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero)
        );

        Assert.True(span.IsActive(now));
    }

    [Fact]
    public void IsActive_WhenEventIsCurrent_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        var span = new EventSpan(
            new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero)
        );

        Assert.True(span.IsActive(now));
    }

    [Fact]
    public void IsActive_WhenEventHasEnded_ReturnsFalse()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero)
        );

        Assert.False(span.IsActive(span.EndDay.AddTicks(1)));
    }

    [Fact]
    public void IsActive_WhenNowEqualsEndDay_ReturnsFalse()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero)
        );

        Assert.False(span.IsActive(span.EndDay));
    }
}
