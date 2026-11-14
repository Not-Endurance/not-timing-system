using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects;
using NoTiming.Domain.Objects;
using NoTiming.IntegrationTests.Drivers;
using NoTiming.IntegrationTests.Infrastructure;

namespace NoTiming.IntegrationTests;

public sealed class FinalizationLockIntegrationTests : IClassFixture<NoTimingIntegrationFixture>
{
    readonly NoTimingIntegrationFixture _fixture;

    public FinalizationLockIntegrationTests(NoTimingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_WhenEventIsFinalized_ShouldRefuseNewOfficial()
    {
        const int eventId = 1801;
        using var api = new NoTimingApiDriver(_fixture.NoTimingBaseUrl);
        await api.Create(CreateEvent(eventId, isFinalized: true));

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => api.Create(IntegrationPayloadFactory.Official(eventId, null))
        );

        Assert.Contains("finalized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenEventIsNotFinalized_ShouldAcceptNewOfficial()
    {
        const int eventId = 1802;
        using var api = new NoTimingApiDriver(_fixture.NoTimingBaseUrl);
        await api.Create(CreateEvent(eventId, isFinalized: false));

        await api.Create(IntegrationPayloadFactory.Official(eventId, null));
    }

    static EventInformation CreateEvent(int eventId, bool isFinalized)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var today = DateTimeOffset.UtcNow.Date.AddDays(-10);
        return new EventInformation(
            country,
            "Finalization Event",
            "Sofia",
            new EventSpan(today, today.AddDays(1), "Europe/Sofia"),
            null,
            eventId,
            isActive: false,
            isFinalized: isFinalized
        );
    }
}
