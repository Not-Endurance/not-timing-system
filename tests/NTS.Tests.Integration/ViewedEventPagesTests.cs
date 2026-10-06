using Microsoft.Playwright;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The pages of an Event name the Event in their address and show what its stage shows (#630, ADR-0007), in a real
/// browser: the Ui, served by the Api, reads the Event through its provider. A Historic Event shows its Rankings and
/// Results and no control of the running Event, and the pages that are about the running Event are not reachable for
/// it. The Ui is a heavy page to load, so one load serves the test and the pages are reached the way the app reaches
/// them, by its own router.
/// </summary>
public sealed class ViewedEventPagesTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    const string NOT_AVAILABLE = "This page is not available for an Event that has ended.";
    const float PATIENCE = 90_000;

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ViewedEventPagesTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_Historic_Event_shows_its_Results_and_none_of_the_pages_of_a_running_Event()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo);
        var historicId = await EventSeed.HistoricAsync(mongo, tenant, null, DateTimeOffset.UtcNow);
        var participations = new[]
        {
            IntegrationPayloadFactory.ActiveParticipation(historicId, 1, Guid.NewGuid()),
            IntegrationPayloadFactory.ActiveParticipation(historicId, 2, Guid.NewGuid()),
        };
        foreach (var participation in participations)
        {
            await EventSeed.ParticipationAsync(mongo, tenant, participation);
        }

        await EventSeed.RankingAsync(
            mongo,
            tenant,
            IntegrationPayloadFactory.Ranking(historicId, participations, Guid.NewGuid(), "Historic Ranking")
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url($"/historic-events/{historicId}"));

        await Expect(page.GetByText("Historic Ranking").First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Startlist", new PageGetByTextOptions { Exact = true })).ToHaveCountAsync(0);
        foreach (var aboutTheRunningEvent in new[] { "startlist", "arrivelist", "presentlist", "performance" })
        {
            await page.EvaluateAsync($"Blazor.navigateTo('/historic-events/{historicId}')");
            await Expect(page.GetByText("Historic Ranking").First).ToBeVisibleAsync(Loaded());
            await page.EvaluateAsync($"Blazor.navigateTo('/events/{historicId}/{aboutTheRunningEvent}')");

            await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded());
        }
    }

    [Fact]
    public async Task An_Event_the_Api_does_not_have_says_so()
    {
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url($"/events/{Guid.NewGuid()}/startlist"));

        await Expect(page.GetByText("Event not found.").First).ToBeVisibleAsync(Loaded());
    }

    /// <summary>The Ui is loaded by the time the first thing of it shows.</summary>
    static LocatorAssertionsToBeVisibleOptions Loaded()
    {
        return new LocatorAssertionsToBeVisibleOptions { Timeout = PATIENCE };
    }

    static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
