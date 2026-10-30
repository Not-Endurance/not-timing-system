using Microsoft.Playwright;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// Who reaches the pages of the Console, in a real browser (#646, ADR-0012): the pages that run an Event are the Main Operator's,
/// and a Tenant Root who assigned the role away, an Operator, an Official and the Developer are shown none of them, whatever the
/// address they type. What a person may do is what the Api says of them, so the pages show what is theirs and nothing more.
/// </summary>
public sealed class ConsoleAccessBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    const string REFUSAL = "Only the Main Operator of an Event opens its Setup.";

    public static TheoryData<string> People()
    {
        var people = new TheoryData<string>();
        foreach (
            var who in new[]
            {
                "a Tenant Root that assigned the role away",
                "an Operator",
                "an Official",
                "the Developer",
            }
        )
        {
            people.Add(who);
        }

        return people;
    }

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleAccessBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Theory]
    [MemberData(nameof(People))]
    public async Task The_pages_that_run_a_Live_Event_are_shown_to_nobody_but_its_Main_Operator(string who)
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString, who);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Person.Email, $"/console/events/{scene.Live}/dashboard");

        foreach (
            var (path, panel) in new[]
            {
                ("dashboard", "console-time-panel"),
                ("handouts", "console-handouts-panel"),
                ("startlist", "console-startlist-panel"),
                ("branding", "console-branding"),
            }
        )
        {
            await page.EvaluateAsync($"Blazor.navigateTo('/console/events/{scene.Live}/{path}')");

            await Expect(page.GetByText(REFUSAL).First).ToBeVisibleAsync(Loaded());
            await Expect(page.GetByTestId(panel)).ToHaveCountAsync(0, Counted());
        }

        // and the home does not offer the dashboard, the Setup or the start of an Event the person does not run, even when it lists the Event
        await page.EvaluateAsync("Blazor.navigateTo('/console')");
        await Expect(page.GetByTestId("console-events").Or(page.GetByTestId("console-no-events")))
            .ToBeVisibleAsync(Loaded());
        if (who.StartsWith("a Tenant Root", StringComparison.Ordinal))
        {
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Results", Exact = true }).First)
                .ToBeVisibleAsync(Loaded()); // the Tenant's Live Event is listed, with the Results that it may read
        }

        if (who.StartsWith("a Tenant Root", StringComparison.Ordinal))
        {
            // the Results of the Live Event that it does not run have the row of buttons of the Event, and say nothing of a connection to it
            await page.EvaluateAsync($"Blazor.navigateTo('/console/events/{scene.Live}/rankings')");
            await Expect(
                    page.GetByTestId("console-event-navigation")
                        .GetByRole(AriaRole.Button, new() { Name = "Ranklist", Exact = true })
                )
                .ToBeDisabledAsync(new() { Timeout = PATIENCE });
            await Expect(page.GetByTestId("console-connection")).ToHaveCountAsync(0, Counted());
            await page.EvaluateAsync("Blazor.navigateTo('/console')");
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Results", Exact = true }).First)
                .ToBeVisibleAsync(Loaded());
        }

        foreach (var name in new[] { "Dashboard", "Setup", "start" })
        {
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }))
                .ToHaveCountAsync(0, Counted());
        }
    }

    [Theory]
    [MemberData(nameof(People))]
    public async Task The_Setup_of_an_Event_that_has_not_started_is_opened_by_nobody_but_its_Main_Operator(string who)
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString, who);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Person.Email, $"/console/events/{scene.Unstarted}/setup");

        await Expect(page.GetByText(REFUSAL).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveCountAsync(0, Counted());
    }

    sealed class Scene
    {
        public static async Task<Scene> CreateAsync(string mongo, string who)
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var live = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
            var unstarted = await EventSeed.SetupAsync(mongo, tenant, mainOperator.Id, "Not yet");
            var person = who switch
            {
                "a Tenant Root that assigned the role away" => await TenancySeed.AccountAsync(
                    mongo,
                    tenant,
                    TenancySeed.TenantRootOf(tenant),
                    name: "Maria Petrova"
                ),
                "the Developer" => await TenancySeed.AccountAsync(mongo, tenant, isDeveloper: true, name: "Dev Eloper"),
                _ => await TenancySeed.AccountAsync(mongo, tenant, name: "Elena Petrova"),
            };
            if (who == "an Operator")
            {
                await GrantAsync(mongo, tenant, live, unstarted, "Operator", null, person);
            }
            else if (who == "an Official")
            {
                await GrantAsync(mongo, tenant, live, unstarted, "Official", "Steward", person);
            }

            return new Scene(person, live, unstarted);
        }

        Scene(TenancySeed.Seeded person, Guid live, Guid unstarted)
        {
            Person = person;
            Live = live;
            Unstarted = unstarted;
        }

        public TenancySeed.Seeded Person { get; }
        public Guid Live { get; }
        public Guid Unstarted { get; }

        static async Task GrantAsync(
            string mongo,
            string tenant,
            Guid live,
            Guid unstarted,
            string kind,
            string? role,
            TenancySeed.Seeded person
        )
        {
            foreach (var eventId in new[] { live, unstarted })
            {
                await EventSeed.GrantAsync(mongo, tenant, eventId, kind, role, person.Email, person.Id);
            }
        }
    }
}
