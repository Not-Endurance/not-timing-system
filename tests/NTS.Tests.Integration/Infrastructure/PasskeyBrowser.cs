using System.Buffers.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A real Chromium with a virtual authenticator, for the passkey ceremonies (#600): the pages of the Api, their script,
/// the browser's WebAuthn API and the host are all real, and only the person's finger is simulated. One browser
/// serves a class; each test opens contexts of its own, and each context has an authenticator of its own, which is
/// what a second device is.
/// </summary>
public sealed class PasskeyBrowserFixture : IAsyncLifetime
{
    IPlaywright? _playwright;
    IBrowser? _browser;

    /// <summary>
    /// A fresh browser profile and a device with its own authenticator. The address must be <c>localhost</c>: a passkey
    /// belongs to a domain, and an IP address is none.
    /// </summary>
    internal async Task<BrowserDevice> OpenAsync(Uri hostAddress, string locale = "en-US")
    {
        var address = new UriBuilder(hostAddress) { Host = "localhost" }.Uri;
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { Locale = locale });
        var page = await context.NewPageAsync();

        // Conditional mediation waits for a person to pick from the autofill list, which nothing automates. The page
        // then offers the button, and the ceremonies go through the modal prompt, which the authenticator answers.
        await page.AddInitScriptAsync(
            "if (window.PublicKeyCredential) { PublicKeyCredential.isConditionalMediationAvailable = async () => false; }"
        );

        var cdp = await context.NewCDPSessionAsync(page);
        await cdp.SendAsync("WebAuthn.enable");
        var added = await cdp.SendAsync(
            "WebAuthn.addVirtualAuthenticator",
            new Dictionary<string, object>
            {
                ["options"] = new Dictionary<string, object>
                {
                    ["protocol"] = "ctap2",
                    ["transport"] = "internal",
                    ["hasResidentKey"] = true,
                    ["hasUserVerification"] = true,
                    ["isUserVerified"] = true,
                    ["automaticPresenceSimulation"] = true,
                },
            }
        );
        return new BrowserDevice(context, page, cdp, added!.Value.GetProperty("authenticatorId").GetString()!, address);
    }

    public async Task InitializeAsync()
    {
        PlaywrightBrowserEnvironment.ConfigureCurrentProcess(RepositoryPaths.Discover());
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}

/// <summary>One browser context: a person's device, with the passkeys it holds in its authenticator.</summary>
internal sealed class BrowserDevice : IAsyncDisposable
{
    readonly ICDPSession _cdp;
    readonly string _authenticatorId;

    public BrowserDevice(IBrowserContext context, IPage page, ICDPSession cdp, string authenticatorId, Uri address)
    {
        Context = context;
        Page = page;
        Address = address;
        _cdp = cdp;
        _authenticatorId = authenticatorId;
    }

    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public Uri Address { get; }

    public string Url(string path)
    {
        return new Uri(Address, path).ToString();
    }

    /// <summary>The credentials the authenticator of this device holds.</summary>
    public async Task<IReadOnlyList<VirtualCredential>> CredentialsAsync()
    {
        var result = await _cdp.SendAsync(
            "WebAuthn.getCredentials",
            new Dictionary<string, object> { ["authenticatorId"] = _authenticatorId }
        );
        return [.. result!.Value.GetProperty("credentials").EnumerateArray().Select(x => new VirtualCredential(x))];
    }

    /// <summary>A request the page makes, with the cookies of the browser: the status and the body.</summary>
    public async Task<(int Status, JsonElement Body)> FetchAsync(string method, string path)
    {
        var result = await Page.EvaluateAsync<JsonElement>(
            @"async ([method, path]) => {
                const response = await fetch(path, { method, credentials: 'same-origin', headers: { Accept: 'application/vnd.api+json' } });
                const text = await response.text();
                return { status: response.status, body: text ? JSON.parse(text) : null };
            }",
            new object[] { method, path }
        );
        return (result.GetProperty("status").GetInt32(), result.GetProperty("body"));
    }

    public async ValueTask DisposeAsync()
    {
        await Context.CloseAsync();
    }
}

/// <summary>What the authenticator keeps for a passkey.</summary>
internal sealed class VirtualCredential
{
    public VirtualCredential(JsonElement credential)
    {
        CredentialId = Convert.FromBase64String(credential.GetProperty("credentialId").GetString()!);
        RelyingPartyId = credential.GetProperty("rpId").GetString()!;
        SignCount = credential.GetProperty("signCount").GetUInt32();
    }

    public byte[] CredentialId { get; }

    /// <summary>The id the way the Api names a passkey: base64url.</summary>
    public string Id => Base64Url.EncodeToString(CredentialId);

    public string RelyingPartyId { get; }
    public uint SignCount { get; }
}
