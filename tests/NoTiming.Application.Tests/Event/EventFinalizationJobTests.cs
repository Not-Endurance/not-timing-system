using NoTiming.Api.Application.Event;

namespace NoTiming.Application.Tests.Event;

public class EventFinalizationJobTests
{
    [Theory]
    [InlineData("2026-11-13T01:30:00Z", "2026-11-13T03:00:00Z")]
    [InlineData("2026-11-13T03:00:00Z", "2026-11-13T15:00:00Z")]
    [InlineData("2026-11-13T10:00:00Z", "2026-11-13T15:00:00Z")]
    [InlineData("2026-11-13T15:00:00Z", "2026-11-14T03:00:00Z")]
    [InlineData("2026-11-13T23:59:00Z", "2026-11-14T03:00:00Z")]
    public void NextRun_ShouldReturnTheFirstRunAfterNow(string now, string expected)
    {
        var next = EventFinalizationJob.NextRun(DateTimeOffset.Parse(now));

        Assert.Equal(DateTimeOffset.Parse(expected), next);
    }
}
