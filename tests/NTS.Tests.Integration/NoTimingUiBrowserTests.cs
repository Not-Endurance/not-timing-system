using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NoTiming.Ui.Storage.REST;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The Ui for viewers, the Snapshot page and the account, in a real browser against the Api that serves it (#645,
/// ADR-0011): a visitor browses the pages of the running Event and sees it change without signing in; a person signs in on
/// the pages of the host and comes back to where they were; an Official sends a Snapshot; and the browser keeps no sign-in
/// of the person in its storage. The Ui is a heavy page to load, so a test loads it as few times as it can and moves in it
/// by its own router.
/// </summary>
public sealed class NoTimingUiBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    const float PATIENCE = 90_000;

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public NoTimingUiBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_visitor_browses_the_pages_of_the_running_Event_and_sees_it_change_without_signing_in()
    {
        var scene = await SceneAsync();
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url($"/events/{scene.EventId}/startlist"));

        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded());
        await page.EvaluateAsync($"Blazor.navigateTo('/events/{scene.EventId}/arrivelist')");
        await Expect(page.GetByText("Arrivelist").First).ToBeVisibleAsync(Loaded());
        Assert.DoesNotContain("Integration Rider", await page.InnerTextAsync("body")); // nobody has arrived

        await scene.SendAsync(SnapshotType.Arrive, 1); // an Official sends a Snapshot, somewhere else

        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded());
        await AssertStorageHoldsNoSignInAsync(page, null);

        await page.GetByText("Sign In").ClickAsync(); // the drawer takes them to the host's page, and back to this one
        var back = Uri.EscapeDataString($"/events/{scene.EventId}/arrivelist");
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl=" + Regex.Escape(back)),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
    }

    [Fact]
    public async Task An_Official_signs_in_on_the_pages_of_the_host_sends_a_Snapshot_and_signs_out_and_the_browser_keeps_no_sign_in()
    {
        var scene = await SceneAsync();
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var snapshotPage = $"/events/{scene.EventId}/snapshot";

        await page.GotoAsync(device.Url(snapshotPage));
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl="),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await SignInOnTheHostPagesAsync(page, scene.Official.Email);
        await page.WaitForURLAsync(device.Url(snapshotPage), new PageWaitForURLOptions { Timeout = PATIENCE });
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());

        await SendSnapshotOfAsync(page, 1);

        await Expect(page.GetByText("Snapshots sent as Arrive")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "History" }).First)
            .ToBeVisibleAsync();
        var sent = Assert.Single(await scene.EventsOfAsync(1));
        Assert.Equal(scene.Official.Id, sent["ActorId"].AsGuid);
        await AssertStorageHoldsNoSignInAsync(page, scene.Official.Email);

        await page.EvaluateAsync("() => { window.__thePageThatWasLoaded = true; }");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Ana" }).ClickAsync();
        await page.GetByText("Sign Out").ClickAsync();

        await Expect(page.GetByText("Sign In")).ToBeVisibleAsync(Loaded()); // the drawer is a visitor's again
        await Expect(page.GetByText("Startlist", new PageGetByTextOptions { Exact = true }).First)
            .ToBeVisibleAsync(Loaded()); // and the app follows the running Event again, as a visitor
        Assert.Equal(401, (await device.FetchAsync("GET", "/api/me")).Status);
        Assert.Equal("undefined", await page.EvaluateAsync<string>("() => typeof window.__thePageThatWasLoaded")); // loaded again
        await AssertStorageHoldsNoSignInAsync(page, scene.Official.Email);
    }

    [Fact]
    public async Task A_registered_person_without_access_to_the_Event_is_taken_from_the_Snapshot_page_and_has_no_link_to_it()
    {
        var scene = await SceneAsync();
        var registered = await scene.RegisteredAsync();
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url($"/events/{scene.EventId}/snapshot"));
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl="),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await SignInOnTheHostPagesAsync(page, registered.Email);

        await page.WaitForURLAsync(
            device.Url($"/events/{scene.EventId}/performance"),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await Expect(page.GetByText("Startlist", new PageGetByTextOptions { Exact = true }).First)
            .ToBeVisibleAsync(Loaded());
        await Expect(
                page.Locator(".mud-nav-link-text")
                    .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^Snapshot$") })
            )
            .ToHaveCountAsync(0);
        Assert.Empty(await scene.EventsOfAsync(1));
    }

    [Fact]
    public async Task A_person_whose_access_was_removed_loses_the_Snapshot_page_at_the_next_request_that_says_so()
    {
        var scene = await SceneAsync();
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var snapshotPage = $"/events/{scene.EventId}/snapshot";
        await page.GotoAsync(device.Url(snapshotPage));
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl="),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await SignInOnTheHostPagesAsync(page, scene.Official.Email);
        await page.WaitForURLAsync(device.Url(snapshotPage), new PageWaitForURLOptions { Timeout = PATIENCE });
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await scene.RemoveGrantAsync(scene.Official.Email); // the Main Operator takes the access away, and the page does not know

        await SendSnapshotOfAsync(page, 1); // the Api refuses it, and says why

        await page.WaitForURLAsync(
            device.Url($"/events/{scene.EventId}/performance"),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        Assert.Empty(await scene.EventsOfAsync(1));
    }

    [Fact]
    public async Task Snapshots_whose_answer_was_lost_are_kept_in_the_browser_and_sent_again_with_the_same_ids_even_after_the_page_was_lost()
    {
        var scene = await SceneAsync();
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var snapshotPage = $"/events/{scene.EventId}/snapshot";
        var sent = new List<string>();
        await page.RouteAsync(
            "**/api/snapshots/actions/send-group",
            async route =>
            {
                sent.Add(route.Request.PostData!);
                if (sent.Count == 1)
                {
                    await route.FetchAsync(); // the server records it, and the answer is lost on the way
                    await route.AbortAsync();
                    return;
                }

                await route.ContinueAsync();
            }
        );
        await page.GotoAsync(device.Url(snapshotPage));
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl="),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await SignInOnTheHostPagesAsync(page, scene.Official.Email);
        await page.WaitForURLAsync(device.Url(snapshotPage), new PageWaitForURLOptions { Timeout = PATIENCE });
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());

        await SendSnapshotOfAsync(page, 1);

        var waiting = page.GetByTestId("snapshots-waiting");
        await Expect(waiting).ToBeVisibleAsync(Loaded()); // the person is told it waits
        Assert.Single(await scene.EventsOfAsync(1)); // and the server has it
        Assert.Single(await KeptKeysAsync(page)); // and so has the browser, as a group with an id
        await page.ReloadAsync(); // the page is lost before the app has sent it again
        await Expect(waiting).ToBeVisibleAsync(Loaded()); // and found again, from what the browser kept
        await Expect(waiting).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = PATIENCE }); // until it is answered

        Assert.Equal(2, sent.Count);
        Assert.Equal(SnapshotIdsOf(sent[0]), SnapshotIdsOf(sent[1])); // the same Snapshots to the server both times
        Assert.Single(await scene.EventsOfAsync(1)); // and a time was recorded once
        Assert.Empty(await KeptKeysAsync(page)); // and the browser keeps nothing of it
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "History" }).First)
            .ToBeVisibleAsync();
        await AssertStorageHoldsNoSignInAsync(page, scene.Official.Email);
    }

    [Fact]
    public async Task The_account_page_holds_the_language_and_the_Federation_choices_and_the_Federation_only_for_a_person_with_more_than_one()
    {
        var mongo = _host.MongoConnectionString;
        var home = await TenancySeed.TenantAsync(mongo, "Home Federation");
        var other = await TenancySeed.TenantAsync(mongo, "Other Federation");
        var oneOnly = await TenancySeed.AccountAsync(mongo, home, name: "Boris Ivanov");
        var several = await TenancySeed.AccountAsync(
            mongo,
            home,
            roles: new Dictionary<string, string[]> { [other] = [] },
            name: "Vera Georgieva"
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await page.GotoAsync(device.Url("/profile"));
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl="),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await SignInOnTheHostPagesAsync(page, oneOnly.Email);
        await page.WaitForURLAsync(device.Url("/profile"), new PageWaitForURLOptions { Timeout = PATIENCE });

        await Expect(page.GetByTestId("account-email"))
            .ToHaveTextAsync(oneOnly.Email, new LocatorAssertionsToHaveTextOptions { Timeout = PATIENCE });
        await Expect(SelectOf(page, "Language")).ToBeVisibleAsync();
        await Expect(SelectOf(page, "Federation")).ToHaveCountAsync(0); // there is nothing to choose between

        await device.FetchAsync("DELETE", "/api/sessions/current");
        await page.GotoAsync(device.Url("/profile"));
        await page.WaitForURLAsync(
            new Regex(@"/sign-in\?returnUrl="),
            new PageWaitForURLOptions { Timeout = PATIENCE }
        );
        await SignInOnTheHostPagesAsync(page, several.Email);
        await page.WaitForURLAsync(device.Url("/profile"), new PageWaitForURLOptions { Timeout = PATIENCE });
        await Expect(SelectOf(page, "Federation")).ToBeVisibleAsync(Loaded());
        await SelectOf(page, "Federation").ClickAsync();
        await page.GetByText("Other Federation").ClickAsync();

        await Eventually(async () =>
        {
            var me = await device.FetchAsync("GET", "/api/me");
            return me.Body.GetProperty("data").GetProperty("attributes").GetProperty("currentTenantId").GetString()
                == other;
        });
        await SelectOf(page, "Language").ClickAsync();
        await page.GetByText("Български").ClickAsync();

        await Expect(page.GetByText("Акаунт").First).ToBeVisibleAsync(Loaded()); // the app is loaded again, in Bulgarian
        Assert.Equal("bg", await page.EvaluateAsync<string>("() => localStorage.getItem('nts.language')"));
        await AssertStorageHoldsNoSignInAsync(page, several.Email);
    }

    /// <summary>A Live Event with three Participations that have started an hour ago, and the people around it.</summary>
    async Task<Scene> SceneAsync()
    {
        var mongo = _host.MongoConnectionString;
        ApiMongo.Configure();
        var tenant = await TenancySeed.TenantAsync(mongo);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant);
        var eventId = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        var started = DateTimeOffset.Now.AddHours(-1);
        var ids = new List<Guid>();
        for (var number = 1; number <= 3; number++)
        {
            var participation = IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                number,
                Guid.NewGuid(),
                startTime: started
            );
            await EventSeed.ParticipationAsync(mongo, tenant, participation);
            ids.Add(participation.Id);
        }

        var client = _host.Api.CreateClient();
        var official = await TenancySeed.SignedInAsync(_host.Api, client, mongo, tenant, name: "Ana Petrova");
        await EventSeed.GrantAsync(mongo, tenant, eventId, "Official", "Steward", official.Email, official.Id);
        return new Scene(_host, tenant, eventId, official, ids);
    }

    /// <summary>
    /// Signs in on the pages of the host as a person does: the address, the code that was mailed to it, and the offer of a
    /// passkey, which they leave for later, so that the host returns them to where they were going.
    /// </summary>
    async Task SignInOnTheHostPagesAsync(IPage page, string email)
    {
        await page.FillAsync("#email", email);
        await page.ClickAsync("#email-form button[type=submit]");
        await page.WaitForSelectorAsync("#code-form:not([hidden])");
        await page.FillAsync("#code", ApiSessions.CodeSentTo(_host.Api, email));
        await page.ClickAsync("#code-form button[type=submit]");
        await page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));
        await page.ClickAsync("#done");
    }

    /// <summary>What a person does to send the Snapshot of a Participation: select it, capture its time, and send it as an Arrive.</summary>
    static async Task SendSnapshotOfAsync(IPage page, int number)
    {
        await ChipOf(page, number).ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Snapshot", Exact = true })
            .ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Arrive", Exact = true }).ClickAsync();
    }

    static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!await condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(await condition(), "The app did not do what was waited for.");
    }

    /// <summary>The keys the app keeps a group that was sent and not answered under.</summary>
    static async Task<string[]> KeptKeysAsync(IPage page)
    {
        return await page.EvaluateAsync<string[]>(
            "() => Object.keys(localStorage).filter(key => key.startsWith('nts.unanswered-snapshots.'))"
        );
    }

    /// <summary>The ids a request of a group sends its Snapshots under.</summary>
    static string[] SnapshotIdsOf(string body)
    {
        using var document = JsonDocument.Parse(body);
        return
        [
            .. document.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString()!),
        ];
    }

    /// <summary>A select of the account page, by its label.</summary>
    static ILocator SelectOf(IPage page, string label)
    {
        return page.Locator(".mud-input-control").Filter(new LocatorFilterOptions { HasText = label });
    }

    static ILocator ChipOf(IPage page, int number)
    {
        return page.Locator(".mud-chip").Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{number}$") });
    }

    /// <summary>
    /// The browser holds nothing of the sign-in of the person: nothing that a library of sign-in leaves (a token, an account,
    /// its keys), and nothing the person is by. What the app keeps there for itself is the language and the Snapshots that
    /// were sent and not answered, which are the device's and name no account of the host.
    /// </summary>
    static async Task AssertStorageHoldsNoSignInAsync(IPage page, string? email)
    {
        var stored = await page.EvaluateAsync<JsonElement>(
            """
            () => ({
                local: Object.entries(localStorage),
                session: Object.entries(sessionStorage),
                cookies: document.cookie,
            })
            """
        );
        var entries = stored
            .GetProperty("local")
            .EnumerateArray()
            .Concat(stored.GetProperty("session").EnumerateArray())
            .Select(x => (Key: x[0].GetString()!, Value: x[1].GetString()!))
            .ToList();

        Assert.All(
            entries,
            entry =>
            {
                Assert.Matches(@"^nts\.(language|unanswered-snapshots\..+)$", entry.Key);
                Assert.DoesNotContain("msal", entry.Key, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotMatch(@"eyJ[A-Za-z0-9_-]{10,}\.", entry.Value); // no token
                Assert.DoesNotContain("access_token", entry.Value, StringComparison.OrdinalIgnoreCase);
                if (email != null)
                {
                    Assert.DoesNotContain(email, entry.Value, StringComparison.OrdinalIgnoreCase);
                }
            }
        );
        Assert.DoesNotContain(ApiSessions.COOKIE_NAME, stored.GetProperty("cookies").GetString()); // the session is not the app's to read
    }

    /// <summary>The time to wait for the Ui, which is loaded by the time the first thing of it shows.</summary>
    static LocatorAssertionsToBeVisibleOptions Loaded()
    {
        return new LocatorAssertionsToBeVisibleOptions { Timeout = PATIENCE };
    }

    static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    sealed class Scene
    {
        readonly ApiHostFixture _host;
        readonly IReadOnlyList<Guid> _participations;

        public Scene(
            ApiHostFixture host,
            string tenant,
            Guid eventId,
            TenancySeed.Person official,
            IReadOnlyList<Guid> participations
        )
        {
            _host = host;
            _participations = participations;
            Tenant = tenant;
            EventId = eventId;
            Official = official;
        }

        public string Tenant { get; }
        public Guid EventId { get; }

        /// <summary>The Official who has been granted the Event, with a session.</summary>
        public TenancySeed.Person Official { get; }

        /// <summary>A person who is signed up and has been granted nothing.</summary>
        public async Task<TenancySeed.Person> RegisteredAsync()
        {
            using var client = _host.Api.CreateClient();
            return await TenancySeed.SignedInAsync(
                _host.Api,
                client,
                _host.MongoConnectionString,
                Tenant,
                name: "Boris Ivanov"
            );
        }

        /// <summary>The Main Operator takes the grant of the person on the Event away.</summary>
        public async Task RemoveGrantAsync(string email)
        {
            await EventSeed.Grants(_host.MongoConnectionString).DeleteManyAsync(new BsonDocument("Email", email));
        }

        /// <summary>The Official sends the Snapshots of the numbers as of now, as a program does.</summary>
        public async Task SendAsync(SnapshotType type, params int[] numbers)
        {
            var publisher = new SnapshotApiPublisher(JsonApiClients.Over(_host.BaseAddress, Official, out _));
            var group = new SnapshotGroup(
                numbers.Select(number => new Snapshot(
                    number,
                    "Integration Rider",
                    null,
                    new Timestamp(DateTimeOffset.Now)
                )),
                type
            );
            var receipts = await publisher.PublishSnapshotsAsync(EventId, group);
            Assert.All(receipts, receipt => Assert.True(receipt.IsRecorded, receipt.ErrorMessage));
        }

        /// <summary>The time events the Participation of the number keeps in its Phase, as the Api stored them.</summary>
        public async Task<List<BsonDocument>> EventsOfAsync(int number)
        {
            var stored = (
                await RegistrySeed.StoredAsync(
                    _host.MongoConnectionString,
                    "event_participations",
                    _participations[number - 1]
                )
            )!;
            return [.. stored["Phases"][0]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
        }
    }
}
