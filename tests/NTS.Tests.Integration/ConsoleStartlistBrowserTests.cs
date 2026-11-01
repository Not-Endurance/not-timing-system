using Microsoft.Playwright;
using MongoDB.Driver;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration;

/// <summary>
/// The Startlist of a Live Event in the Console, in a real browser (#646): the starts that have a time, by stage, printed
/// through the browser, and the page shown to nobody but the Console Operator.
/// </summary>
public sealed class ConsoleStartlistBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleStartlistBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Startlist_of_the_Event_is_shown_by_stage_and_printed_through_the_browser()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var eventId = await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
        var start = new DateTimeOffset(DateTime.Today.AddHours(6));
        await EventSeed.ParticipationAsync(mongo, tenant, TwoPhases(eventId, 1, start, complete: false));
        await EventSeed.ParticipationAsync(mongo, tenant, TwoPhases(eventId, 2, start.AddMinutes(30), complete: true));
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, consoleOperator.Email, $"/console/events/{eventId}/startlist");

        // the starts of the first stage, and the one of the second that the Participation which completed the first has
        await Expect(page.GetByText("Stage 1", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Stage 2", new() { Exact = true }).First).ToBeVisibleAsync();
        await Expect(page.GetByText("06:00:00").First).ToBeVisibleAsync();
        await Expect(page.GetByText("06:30:00").First).ToBeVisibleAsync();
        await Expect(page.GetByText("10:25:00").First).ToBeVisibleAsync(); // out of the first stage: 09:45 and the rest of 40 minutes

        // printing hands the browser the sheet in a page of its own
        var panel = page.GetByTestId("console-startlist-panel");
        await panel.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await Eventually(async () => page.Frames.Any(x => x != page.MainFrame));
        var printed = page.Frames.First(x => x != page.MainFrame);
        await Eventually(async () => (await printed.ContentAsync()).Contains("Athlete 2", StringComparison.Ordinal));
        var html = await printed.ContentAsync();
        Assert.Contains("print-document-startlist", html);
        Assert.Contains("Athlete 1", html);
    }

    [Fact]
    public async Task A_person_who_does_not_run_the_Event_is_shown_no_Startlist()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var stranger = await TenancySeed.AccountAsync(mongo, tenant, name: "Elena Petrova");
        var eventId = await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
        await EventSeed.ParticipationAsync(
            mongo,
            tenant,
            TwoPhases(eventId, 1, new DateTimeOffset(DateTime.Today.AddHours(6)), complete: false)
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, stranger.Email, $"/console/events/{eventId}/startlist");

        await Expect(
                page.GetByText(
                    "Only a Console Operator of an Event, or an administrator of the federation, opens its Setup."
                ).First
            )
            .ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Athlete 1")).ToHaveCountAsync(0, Counted());
    }

    static Participation TwoPhases(Guid eventId, int number, DateTimeOffset start, bool complete)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(
            $"Athlete {number}",
            null,
            country,
            null,
            (100000 + number).ToString(),
            Guid.NewGuid()
        );
        var horse = new Horse($"Horse {number}", null, (200000 + number).ToString(), Guid.NewGuid());
        var combination = new Combination(number, athlete, horse, null, "80", null, null, Guid.NewGuid());
        var first = new Phase(
            "GATE1/40",
            40,
            40,
            40,
            CompetitionRuleset.FEI,
            false,
            null,
            Timestamp.Create(start),
            complete ? Timestamp.Create(start.AddHours(3)) : null,
            complete ? Timestamp.Create(start.AddHours(3.25)) : null,
            null,
            false,
            false,
            false
        );
        var last = new Phase(
            "GATE2/40",
            40,
            40,
            null,
            CompetitionRuleset.FEI,
            true,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            false
        );
        return new Participation(
            ParticipationCategory.Senior,
            new Competition("CEI 1*", CompetitionRuleset.FEI),
            combination,
            new PhaseCollection([first, last]),
            null,
            eventId,
            Guid.NewGuid()
        );
    }
}
