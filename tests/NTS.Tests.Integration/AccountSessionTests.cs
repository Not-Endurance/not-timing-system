using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using NoTiming.Ui.Features.Account;
using NTS.Contracts;
using NTS.Contracts.Features.Account;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Who the Ui is looking at (#645, ADR-0002): the Ui asks the host who is signed in, as the session cookie the browser keeps
/// says, and derives what to show from the answer. It holds no token and no authentication state of its own: a visitor is
/// nobody until the host says otherwise, and signing out is the host ending the session. Over the real Api in this process.
/// </summary>
public sealed class AccountSessionTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public AccountSessionTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_visitor_who_has_not_signed_in_is_nobody()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var session = new AccountSession(JsonApiClients.Of(api, null, out var asked));

        await session.Load();

        Assert.Null(session.Current);
        Assert.False(session.IsSignedIn);
        Assert.Equal(["GET /api/me"], asked.Asked);
    }

    [Fact]
    public async Task A_person_who_signed_in_is_their_account_and_the_Tenants_they_stand_in()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var other = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            home,
            new Dictionary<string, string[]> { [home] = ["tenant-root"], [other] = [] },
            name: "Ana Petrova"
        );
        using var session = new AccountSession(JsonApiClients.Of(api, person, out _));

        await session.Load();

        Assert.True(session.IsSignedIn);
        var account = Assert.IsType<CurrentAccount>(session.Current);
        Assert.Equal(person.Id, account.Id);
        Assert.Equal(person.Email, account.Email);
        Assert.Equal("Ana Petrova", account.Name);
        Assert.Equal(home, account.HomeTenantId);
        Assert.Equal(home, account.CurrentTenantId);
        Assert.Null(account.SelectedTenantId);
        Assert.False(account.IsDeveloper);
        Assert.Equal([home, other], account.Memberships.Select(x => x.TenantId));
        Assert.Equal(["tenant-root"], account.Memberships[0].Roles);
        Assert.Empty(account.Memberships[1].Roles);
    }

    [Fact]
    public async Task The_Developer_is_told_by_the_host()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null, isDeveloper: true);
        using var session = new AccountSession(JsonApiClients.Of(api, person, out _));

        await session.Load();

        Assert.True(session.Current!.IsDeveloper);
        Assert.Empty(session.Current.Memberships);
    }

    [Fact]
    public async Task A_session_the_host_no_longer_knows_is_nobody_on_the_next_load()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home);
        using var session = new AccountSession(JsonApiClients.Of(api, person, out _));
        await session.Load();
        Assert.True(session.IsSignedIn);
        await person.Page.DeleteAsync("/api/sessions/current"); // signed out on another device

        await session.Refresh();

        Assert.Null(session.Current);
        Assert.False(session.IsSignedIn);
    }

    [Fact]
    public async Task Signing_out_ends_the_session_at_the_host_forgets_the_account_and_tells_who_watches()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home);
        using var session = new AccountSession(JsonApiClients.Of(api, person, out var asked));
        await session.Load();
        var told = 0;
        session.ObservableEvent.Subscribe(() => told++);

        await session.SignOut();

        Assert.Null(session.Current);
        Assert.False(session.IsSignedIn);
        Assert.True(told > 0);
        Assert.Contains("DELETE /api/sessions/current", asked.Asked);
        using var after = new AccountSession(JsonApiClients.Of(api, person, out _));
        await after.Load();
        Assert.Null(after.Current); // the cookie no longer signs in: the host ended the session
    }

    [Fact]
    public async Task Selecting_a_Tenant_changes_where_the_person_reads_and_writes_and_a_Tenant_they_are_not_in_is_refused()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var other = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var stranger = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            home,
            new Dictionary<string, string[]> { [other] = [] }
        );
        using var session = new AccountSession(JsonApiClients.Of(api, person, out _));
        await session.Load();

        await session.SelectTenant(other);
        var selected = session.Current!;
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => session.SelectTenant(stranger));
        await session.SelectTenant(null);

        Assert.Equal(other, selected.SelectedTenantId);
        Assert.Equal(other, selected.CurrentTenantId);
        Assert.Contains("not-a-member", refused.Message);
        Assert.Null(session.Current!.SelectedTenantId);
        Assert.Equal(home, session.Current.CurrentTenantId);
    }

    [Fact]
    public async Task A_host_that_cannot_be_reached_leaves_the_account_unknown_and_is_asked_again_on_the_next_load()
    {
        var host = new Down();
        using var session = new AccountSession(host.Client());

        await session.Load();
        var whileDown = session.Current;
        host.IsDown = false;
        await session.Load();

        Assert.Null(whileDown);
        Assert.Equal(2, host.Asked); // an unreachable host is not a visitor: it is asked again, not remembered
    }

    [Theory]
    [InlineData(
        "/events/3f2504e0-4f89-41d3-9a0c-0305e82c3301/snapshot",
        "/sign-in?returnUrl=%2Fevents%2F3f2504e0-4f89-41d3-9a0c-0305e82c3301%2Fsnapshot"
    )]
    [InlineData("/startlist?x=1&y=2", "/sign-in?returnUrl=%2Fstartlist%3Fx%3D1%26y%3D2")]
    [InlineData("/", "/sign-in")]
    [InlineData("", "/sign-in")]
    [InlineData("https://evil.example/steal", "/sign-in")]
    [InlineData("//evil.example/steal", "/sign-in")]
    public void The_server_page_to_sign_in_returns_to_a_page_of_the_app_and_to_no_other_site(
        string back,
        string expected
    )
    {
        Assert.Equal(expected, AccountSession.SignInUrl(back));
    }

    sealed class Down : IHttpClientFactory
    {
        public bool IsDown { get; set; } = true;
        public int Asked { get; private set; }

        public JsonApiClient Client()
        {
            return new JsonApiClient(this, Options.Create(new JsonApiSettings { Url = "https://localhost/api" }));
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new Throwing(this));
        }

        sealed class Throwing : HttpMessageHandler
        {
            readonly Down _host;

            public Throwing(Down host)
            {
                _host = host;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                _host.Asked++;
                return _host.IsDown
                    ? throw new HttpRequestException("The host cannot be reached.")
                    : Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
            }
        }
    }
}
