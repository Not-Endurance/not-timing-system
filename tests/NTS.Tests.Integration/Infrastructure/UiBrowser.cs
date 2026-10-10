using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>What the browser tests of the Ui share: the patience, the sign-in on the pages of the host and what to look at.</summary>
internal static class UiBrowser
{
    public const float PATIENCE = 90_000;

    /// <summary>
    /// Signs in on the pages of the host as a person does: the address, the code that was mailed to it, and the offer of a
    /// passkey, which they leave for later, so that the host returns them to where they were going.
    /// </summary>
    public static async Task SignInOnTheHostPagesAsync(ApiHostFixture host, IPage page, string email)
    {
        await page.FillAsync("#email", email);
        await page.ClickAsync("#email-form button[type=submit]");
        await page.WaitForSelectorAsync("#code-form:not([hidden])");
        await page.FillAsync("#code", ApiSessions.CodeSentTo(host.Api, email));
        await page.ClickAsync("#code-form button[type=submit]");
        await page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));
        await page.ClickAsync("#done");
    }

    /// <summary>What a person does to send the Snapshot of a Participation: select it, capture its time, and send it as an Arrive.</summary>
    public static async Task SendSnapshotOfAsync(IPage page, int number)
    {
        await ChipOf(page, number).ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Snapshot", Exact = true })
            .ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Arrive", Exact = true }).ClickAsync();
    }

    public static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!await condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(await condition(), "The app did not do what was waited for.");
    }

    /// <summary>The keys the app keeps a group that was sent and not answered under.</summary>
    public static async Task<string[]> KeptKeysAsync(IPage page)
    {
        return await page.EvaluateAsync<string[]>(
            "() => Object.keys(localStorage).filter(key => key.startsWith('nts.unanswered-snapshots.'))"
        );
    }

    /// <summary>The ids a request of a group sends its Snapshots under.</summary>
    public static string[] SnapshotIdsOf(string body)
    {
        using var document = JsonDocument.Parse(body);
        return
        [
            .. document.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString()!),
        ];
    }

    /// <summary>A select of the account page, by its label.</summary>
    public static ILocator SelectOf(IPage page, string label)
    {
        return page.Locator(".mud-input-control").Filter(new LocatorFilterOptions { HasText = label });
    }

    public static ILocator ChipOf(IPage page, int number)
    {
        return page.Locator(".mud-chip").Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{number}$") });
    }

    /// <summary>
    /// The browser holds nothing of the sign-in of the person: nothing that a library of sign-in leaves (a token, an account,
    /// its keys), and nothing the person is by. What the app keeps there for itself is the language and the Snapshots that
    /// were sent and not answered, which are the device's and name no account of the host.
    /// </summary>
    public static async Task AssertStorageHoldsNoSignInAsync(IPage page, string? email)
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
    public static LocatorAssertionsToBeVisibleOptions Loaded()
    {
        return new LocatorAssertionsToBeVisibleOptions { Timeout = PATIENCE };
    }

    /// <summary>The time to wait for a count that is to change, which the Ui is loaded by when it does.</summary>
    public static LocatorAssertionsToHaveCountOptions Counted()
    {
        return new LocatorAssertionsToHaveCountOptions { Timeout = PATIENCE };
    }

    public static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
