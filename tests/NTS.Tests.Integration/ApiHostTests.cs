using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Live;
using NTS.Contracts;
using NTS.Contracts.Live;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// NoTiming.Api on its own, in this process on a real loopback port (ADR-0011, ADR-0013), on a MongoDB in a container.
/// </summary>
public sealed class ApiHostTests : IClassFixture<ApiHostFixture>
{
    static readonly TimeSpan PATIENCE = TimeSpan.FromSeconds(10);
    static readonly Guid AN_EVENT = TestId.Of(7);
    static readonly Guid ANOTHER_EVENT = TestId.Of(8);
    static readonly Guid A_PARTICIPATION = TestId.Of(42);

    readonly ApiHostFixture _host;
    readonly HttpClient _http;

    public ApiHostTests(ApiHostFixture host)
    {
        _host = host;
        _http = new HttpClient { BaseAddress = host.BaseAddress };
    }

    [Fact]
    public async Task Health_answers_ok_and_names_the_hub()
    {
        var response = await _http.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("/live-hub", body.GetProperty("hub").GetString());
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/")]
    [InlineData("/api/anything")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        var response = await _http.GetAsync(path);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        Assert.True(response.Headers.Contains("Cross-Origin-Opener-Policy"));
        Assert.StartsWith(
            "default-src 'self'",
            response.Headers.GetValues("Content-Security-Policy-Report-Only").Single()
        );
        Assert.False(
            response.Headers.Contains("Content-Security-Policy"),
            "The policy is report-only until the Ui has been watched under it."
        );
    }

    [Fact]
    public async Task A_deep_link_serves_the_Ui()
    {
        var response = await _http.GetAsync("/startlist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<base href=\"/\"", html);
        Assert.Contains("<title>NoTiming</title>", html);
    }

    [Fact]
    public async Task An_unknown_api_route_answers_a_json_not_found_and_never_the_Ui()
    {
        var response = await _http.GetAsync("/api/anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/vnd.api+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not-found", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_missing_file_stays_a_404()
    {
        var response = await _http.GetAsync("/missing.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_client_receives_the_change_notifications_of_its_Event_only()
    {
        await using var inTheEvent = ConnectToEvent(AN_EVENT);
        await using var elsewhere = ConnectToEvent(ANOTHER_EVENT);
        var received = new TaskCompletionSource<(Guid EventId, Guid ParticipationId)>();
        var missed = new List<(Guid, Guid)>();
        inTheEvent.On<Guid, Guid>(
            nameof(ILiveClientProcedures.ParticipationChanged),
            (eventId, participationId) => received.TrySetResult((eventId, participationId))
        );
        elsewhere.On<Guid, Guid>(
            nameof(ILiveClientProcedures.ParticipationChanged),
            (eventId, participationId) => missed.Add((eventId, participationId))
        );
        await inTheEvent.StartAsync();
        await elsewhere.StartAsync();

        var hub = _host.Api.Services.GetRequiredService<IHubContext<LiveHub, ILiveClientProcedures>>();
        await hub.Clients.Group(LiveGroup.Name(AN_EVENT)).ParticipationChanged(AN_EVENT, A_PARTICIPATION);

        Assert.Equal((AN_EVENT, A_PARTICIPATION), await received.Task.WaitAsync(PATIENCE));
        await Task.Delay(300);
        Assert.Empty(missed);
    }

    [Fact]
    public async Task The_hub_has_no_method_a_client_can_call()
    {
        var callable = typeof(LiveHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name is not (nameof(Hub.OnConnectedAsync) or nameof(Hub.OnDisconnectedAsync)));
        Assert.Empty(callable);

        await using var connection = ConnectToEvent(AN_EVENT);
        await connection.StartAsync();
        var refused = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Receive", 7));
        Assert.Contains("Method does not exist", refused.Message);
    }

    [Fact]
    public async Task The_hub_refuses_a_connection_that_names_no_Event()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_host.BaseAddress, ApplicationConstants.LIVE_HUB))
            .AddNewtonsoftJsonProtocol()
            .Build();

        await AssertRefused(connection);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("not-an-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task The_hub_refuses_a_connection_whose_Event_is_not_an_Event_id(string group)
    {
        await using var connection = ConnectToGroup(group);

        await AssertRefused(connection);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("B")]
    [InlineData("D-upper")]
    public async Task The_spelling_of_the_Event_id_does_not_decide_the_group(string format)
    {
        var spelled = format == "D-upper" ? AN_EVENT.ToString().ToUpperInvariant() : AN_EVENT.ToString(format);
        await using var connection = ConnectToGroup(Uri.EscapeDataString(spelled));
        var received = new TaskCompletionSource<(Guid, Guid)>();
        connection.On<Guid, Guid>(
            nameof(ILiveClientProcedures.ParticipationChanged),
            (eventId, participationId) => received.TrySetResult((eventId, participationId))
        );
        await connection.StartAsync();

        var hub = _host.Api.Services.GetRequiredService<IHubContext<LiveHub, ILiveClientProcedures>>();
        await hub.Clients.Group(LiveGroup.Name(AN_EVENT)).ParticipationChanged(AN_EVENT, A_PARTICIPATION);

        Assert.Equal((AN_EVENT, A_PARTICIPATION), await received.Task.WaitAsync(PATIENCE));
    }

    [Fact]
    public async Task A_WebSocket_from_a_foreign_origin_is_refused()
    {
        var address = await OpenWebSocketAddress(AN_EVENT);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://evil.example");

        var refused = await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(address, default));

        Assert.Contains("403", refused.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("this host")]
    [InlineData("http://localhost:7000")]
    public async Task A_WebSocket_without_an_origin_from_this_host_or_from_an_allowed_origin_connects(string? origin)
    {
        var address = await OpenWebSocketAddress(AN_EVENT);
        using var socket = new ClientWebSocket();
        if (origin != null)
        {
            socket.Options.SetRequestHeader(
                "Origin",
                origin == "this host" ? _host.BaseAddress.GetLeftPart(UriPartial.Authority) : origin
            );
        }

        await socket.ConnectAsync(address, default);

        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Theory]
    [InlineData("https://evil.example", false)]
    [InlineData("http://localhost:7000", true)]
    public async Task Cross_origin_requests_to_the_hub_follow_the_allowed_origins(string origin, bool allowed)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, $"{ApplicationConstants.LIVE_HUB}/negotiate");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _http.SendAsync(request);

        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
    }

    [Fact]
    public async Task Outside_development_http_is_redirected_to_https_and_https_carries_HSTS()
    {
        await using var api = new ApiFactory(
            _host.MongoConnectionString,
            environment: "Production",
            configureHost: builder => builder.UseSetting("https_port", "443")
        );

        var plain = api.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://app.example"),
            }
        );
        var redirect = await plain.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.Equal("https://app.example/healthz", redirect.Headers.Location?.ToString());

        var secure = api.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://app.example") }
        );
        var response = await secure.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("max-age=", response.Headers.GetValues("Strict-Transport-Security").Single());
    }

    [Fact]
    public async Task In_development_http_is_served_as_it_is_without_HSTS()
    {
        var response = await _http.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task The_session_cookie_is_the_only_way_in_and_a_bearer_token_or_a_test_header_signs_nobody_in()
    {
        var schemes = await _host.Api.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        // The session cookie, and the short-lived cookie that holds the challenge of a passkey ceremony: nothing signs in with that one.
        Assert.Equal(["Identity.Application", "Identity.TwoFactorUserId"], schemes.Select(x => x.Name));

        var oldBearerToken = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        oldBearerToken.Headers.Authorization = new(
            "Bearer",
            "integration|official@integration.test|Official|nts-client-scope"
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(oldBearerToken)).StatusCode);

        var testHeader = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        testHeader.Headers.Add("X-Test-User", "official@integration.test|Official");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(testHeader)).StatusCode);
    }

    [Fact]
    public async Task The_cookie_container_of_the_login_helper_works_for_REST_and_SignalR_clients_on_a_real_port()
    {
        var email = UserSeed.NewEmail("real-port");
        await UserSeed.AddLegacyUserAsync(_host.MongoConnectionString, email);
        var cookie = await ApiSessions.SignInAsync(_host.Api, _http, email);
        var container = cookie.ToContainer(_host.BaseAddress);

        using var handler = new HttpClientHandler { CookieContainer = container };
        using var rest = new HttpClient(handler) { BaseAddress = _host.BaseAddress };
        var me = await rest.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(
            email,
            (await ApiSessions.ReadJsonAsync(me))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("email")
                .GetString()
        );

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(_host.BaseAddress, $"{ApplicationConstants.LIVE_HUB}?connectionGroup={AN_EVENT}"),
                options => options.Cookies = container
            )
            .AddNewtonsoftJsonProtocol()
            .Build();
        await connection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    async Task AssertRefused(HubConnection connection)
    {
        var closed = new TaskCompletionSource<Exception?>();
        connection.Closed += error =>
        {
            closed.TrySetResult(error);
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync();
        }
        catch (Exception)
        {
            return; // refused while connecting
        }

        Assert.NotNull(await closed.Task.WaitAsync(PATIENCE));
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    HubConnection ConnectToEvent(Guid eventId)
    {
        return ConnectToGroup(eventId.ToString());
    }

    HubConnection ConnectToGroup(string group)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_host.BaseAddress, $"{ApplicationConstants.LIVE_HUB}?connectionGroup={group}"))
            .AddNewtonsoftJsonProtocol()
            .Build();
    }

    async Task<Uri> OpenWebSocketAddress(Guid eventId)
    {
        var negotiated = await _http.PostAsync(
            $"{ApplicationConstants.LIVE_HUB}/negotiate?negotiateVersion=1&connectionGroup={eventId}",
            content: null
        );
        negotiated.EnsureSuccessStatusCode();
        var token = (await negotiated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("connectionToken")
            .GetString();
        var baseAddress = new UriBuilder(_host.BaseAddress) { Scheme = "ws" }.Uri;
        return new Uri(baseAddress, $"{ApplicationConstants.LIVE_HUB}?id={token}&connectionGroup={eventId}");
    }
}
