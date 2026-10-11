using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The federation in the Console, in a real browser (#646, ADR-0012): a Tenant Root reaches the rules of the Regional competitions
/// from the home of the Console and saves them; a person who is not one is not offered them and is told so when they ask.
/// </summary>
public sealed class ConsoleFederationBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleFederationBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_Tenant_Root_opens_the_rules_of_its_federation_from_the_home_and_saves_them()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var root = await TenancySeed.AccountAsync(
            mongo,
            tenant,
            TenancySeed.TenantRootOf(tenant),
            name: "Maria Petrova"
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, root.Email, "/console");

        await page.GetByRole(AriaRole.Button, new() { Name = "Federation", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(device.Url("/console/federation"), new() { Timeout = PATIENCE });
        var rules = page.GetByTestId("console-federation-rules");
        await Expect(rules.GetByLabel("Code of the regional ranker")).ToBeVisibleAsync(Loaded());
        await rules.GetByLabel("Judge the speed of a loop on its average only").ClickAsync();
        await rules.GetByLabel("Code of the regional ranker").FillAsync("BG-2026");
        await rules.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Expect(page.GetByText("The rules were saved.")).ToBeVisibleAsync();
        await Eventually(async () => await StoredRulesAsync(mongo, tenant) == (true, "BG-2026"));

        // and they are what the page shows when it is opened again
        await page.ReloadAsync();
        await Expect(rules.GetByLabel("Code of the regional ranker")).ToHaveValueAsync("BG-2026", Filled());
    }

    [Fact]
    public async Task A_person_who_is_not_a_Tenant_Root_is_offered_no_rules_and_is_told_they_are_not_theirs()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var plain = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        await EventSeed.LiveAsync(mongo, tenant, plain.Id, DateTimeOffset.UtcNow);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, plain.Email, "/console");

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dashboard", Exact = true }))
            .ToBeVisibleAsync(Loaded());
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Federation", Exact = true }))
            .ToHaveCountAsync(0, Counted());
        await page.EvaluateAsync("Blazor.navigateTo('/console/federation')");

        await Expect(page.GetByText("Only an administrator of the federation edits it.").First)
            .ToBeVisibleAsync(Loaded());
        await Expect(page.GetByLabel("Code of the regional ranker")).ToHaveCountAsync(0, Counted());
    }

    static async Task<(bool OnlyAverageLoopSpeed, string? RankerCode)> StoredRulesAsync(string mongo, string tenant)
    {
        var stored = await TenancySeed.Tenants(mongo).Find(new BsonDocument("_id", tenant)).FirstAsync();
        if (!stored.TryGetValue("RegionalRules", out var value) || value is not BsonDocument rules)
        {
            return (false, null);
        }

        return (
            rules.GetValue("OnlyAverageLoopSpeed", false).AsBoolean,
            rules.TryGetValue("RankerCode", out var code) && code.IsString ? code.AsString : null
        );
    }
}
