using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The logos in the Console, in a real browser (#646, ADR-0012): a Tenant Root puts a file in the place of one of the federation's
/// logos and takes it away, a file that no server would take is said so, a Console Operator puts its own in the place of an Event's
/// and has the federation's back, and what is printed has the logo at its head.
/// </summary>
public sealed class ConsoleBrandingBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    // a PNG of one pixel, which is an image to a browser and to the Api alike
    static readonly byte[] PNG = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
    );
    static readonly byte[] OTHER_PNG = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
    );

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleBrandingBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_Tenant_Root_puts_a_logo_in_the_place_of_one_of_the_federation_and_takes_it_away_and_a_file_that_is_no_image_is_refused()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var root = await TenancySeed.AccountAsync(mongo, tenant, TenancySeed.TenantRootOf(tenant), name: "Rada Root");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, root.Email, "/console/federation");
        var left = page.GetByTestId("logo-left");
        await Expect(left).ToBeVisibleAsync(Loaded());
        await Expect(left.GetByText("No logo")).ToBeVisibleAsync();

        // a file of the kinds a browser prints is the logo, and is shown
        await left.Locator("input[type=file]").SetInputFilesAsync(Payload("logo.png", "image/png", PNG));
        await Expect(page.GetByText("The logo was saved.")).ToBeVisibleAsync();
        await Expect(page.GetByTestId("logo-left-image")).ToBeVisibleAsync();
        await Eventually(
            async () =>
                (await StoredAsync(mongo, $"tenant:{tenant}"))
                    ?["Left"]["Content"]
                    .AsBsonBinaryData.Bytes.SequenceEqual(PNG) == true
        );

        // a file that says it is an image and is not, is refused by the Api in its words, and nothing changes
        await left.Locator("input[type=file]")
            .SetInputFilesAsync(Payload("fake.png", "image/png", "This is not an image."u8.ToArray()));
        await Expect(page.GetByText("The content is not an image of the contentType it names.")).ToBeVisibleAsync();
        Assert.True(
            (await StoredAsync(mongo, $"tenant:{tenant}"))!["Left"]["Content"].AsBsonBinaryData.Bytes.SequenceEqual(PNG)
        );

        // one that weighs more than the Api would take is refused before it is sent
        await left.Locator("input[type=file]")
            .SetInputFilesAsync(Payload("large.png", "image/png", new byte[512 * 1024 + 1]));
        await Expect(page.GetByText("A logo weighs at most 512 KiB.")).ToBeVisibleAsync();

        // a kind that is not printed, the same
        await left.Locator("input[type=file]")
            .SetInputFilesAsync(
                Payload("logo.svg", "image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray())
            );
        await Expect(page.GetByText("Choose a PNG, JPEG, GIF or WebP image.")).ToBeVisibleAsync();

        // and it is taken away
        await left.GetByRole(AriaRole.Button, new() { Name = "Remove the logo", Exact = true }).ClickAsync();
        await Expect(page.GetByText("The logo was removed.")).ToBeVisibleAsync();
        await Expect(left.GetByText("No logo")).ToBeVisibleAsync();
        await Eventually(async () => (await StoredAsync(mongo, $"tenant:{tenant}"))?.Contains("Left") != true);
    }

    [Fact]
    public async Task A_Console_Operator_puts_a_logo_of_its_Event_in_place_of_the_federations_has_it_back_and_what_is_printed_has_the_logo_at_its_head()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var eventId = await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 5, Guid.NewGuid());
        await EventSeed.ParticipationAsync(mongo, tenant, participation);
        await SeedTenantLogoAsync(mongo, tenant, PNG);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, consoleOperator.Email, $"/console/events/{eventId}/dashboard");

        // the Event is printed with the logo of the federation until it has one of its own
        await page.GetByTestId("console-event-navigation")
            .GetByRole(AriaRole.Button, new() { Name = "Logos", Exact = true })
            .ClickAsync();
        var left = page.GetByTestId("logo-left");
        await Expect(left.GetByText("The logo of the federation")).ToBeVisibleAsync(Loaded());
        await Expect(left.GetByRole(AriaRole.Button, new() { Name = "Use the logo of the federation" }))
            .ToHaveCountAsync(0, Counted());

        await left.Locator("input[type=file]").SetInputFilesAsync(Payload("event.png", "image/png", OTHER_PNG));
        await Expect(left.GetByText("The logo of this Event")).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("logo-right").GetByText("No logo")).ToBeVisibleAsync();

        // a document of the Event is printed with the logo at its head
        await page.GetByTestId("console-event-navigation")
            .GetByRole(AriaRole.Button, new() { Name = "Handouts", Exact = true })
            .ClickAsync();
        var handouts = page.GetByTestId("console-handouts-panel");
        await handouts.GetByLabel("Combination").FillAsync("Integration");
        await page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new() { HasText = "Integration Rider" })
            .First.ClickAsync();
        await handouts.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded());
        await handouts.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await Eventually(async () => page.Frames.Any(x => x != page.MainFrame));
        var printed = page.Frames.First(x => x != page.MainFrame);
        await Eventually(
            async () => (await printed.ContentAsync()).Contains("Integration Rider", StringComparison.Ordinal)
        );
        var html = await printed.ContentAsync();
        Assert.Contains($"data:image/png;base64,{Convert.ToBase64String(OTHER_PNG)}", html);
        Assert.DoesNotContain(Convert.ToBase64String(PNG), html);
        await page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })
            .ClickAsync();

        // taking the Event's own away gives back the federation's
        await page.GetByTestId("console-event-navigation")
            .GetByRole(AriaRole.Button, new() { Name = "Logos", Exact = true })
            .ClickAsync();
        await left.GetByRole(AriaRole.Button, new() { Name = "Use the logo of the federation" }).ClickAsync();
        await Expect(left.GetByText("The logo of the federation")).ToBeVisibleAsync(Loaded());
        await Eventually(async () => (await StoredAsync(mongo, $"event:{eventId}"))?.Contains("Left") != true);
    }

    static FilePayload Payload(string name, string mimeType, byte[] content)
    {
        return new FilePayload
        {
            Name = name,
            MimeType = mimeType,
            Buffer = content,
        };
    }

    static async Task SeedTenantLogoAsync(string mongo, string tenant, byte[] image)
    {
        await RegistrySeed
            .Collection(mongo, "brandings")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", $"tenant:{tenant}" },
                    { "TenantId", tenant },
                    {
                        "Left",
                        new BsonDocument { { "ContentType", "image/png" }, { "Content", new BsonBinaryData(image) } }
                    },
                }
            );
    }

    static async Task<BsonDocument?> StoredAsync(string mongo, string key)
    {
        return await RegistrySeed
            .Collection(mongo, "brandings")
            .Find(new BsonDocument("_id", key))
            .FirstOrDefaultAsync();
    }
}
