using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Access;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The Api denies by default (#602, ADR-0001, ADR-0012, the rest-api skill): every endpoint answers 401 to a caller who
/// is not signed in unless the one list of public endpoints names it, and a signed-in caller changes state only with
/// the header that a browser cannot send to another origin unless that origin is allowed. The list is enumerated here
/// from the real endpoints, so adding a public endpoint is a change to the list and to this test, and nothing else
/// can make an endpoint public.
/// </summary>
public sealed class DenyByDefaultTests : IClassFixture<MongoFixture>
{
    // What an anonymous caller may reach: the views of ADR-0001 and the surface where a person signs in or registers.
    // An unknown API route answers 404 to everyone, and the hub has no method a client can call.
    static readonly string[] PUBLIC =
    [
        "public-read * api/{**rest}",
        "public-read * live-hub",
        "public-read * live-hub/negotiate",
        "public-read GET api/events",
        "public-read GET api/events/historic",
        "public-read GET api/events/live",
        "public-read GET api/events/{id}",
        "public-read GET api/handouts",
        "public-read GET api/handouts/{id}",
        "public-read GET api/officials",
        "public-read GET api/officials/{id}",
        "public-read GET api/participations",
        "public-read GET api/participations/{id}",
        "public-read GET api/rankings",
        "public-read GET api/rankings/{id}",
        "public-read GET healthz",
        "public-read GET past-events",
        "public-read GET past-events/{eventId:guid}",
        "public-read GET,HEAD {**path:nonfile}",
        "sign-in DELETE api/sessions/current",
        "sign-in GET account/assets/{name}",
        "sign-in GET account/passkeys",
        "sign-in GET privacy",
        "sign-in GET register",
        "sign-in GET sign-in",
        "sign-in POST api/code-challenges",
        "sign-in POST api/passkeys/actions/request-options",
        "sign-in POST api/registrations",
        "sign-in POST api/sessions",
    ];

    readonly MongoFixture _mongo;

    public DenyByDefaultTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_endpoints_that_an_anonymous_caller_may_reach_are_exactly_the_ones_on_the_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);

        var actual = EndpointsOf(api)
            .Where(x => x.Access != "protected")
            .Select(x => $"{x.Access} {x.Methods} {x.Pattern}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(PUBLIC.Order(StringComparer.Ordinal), actual);
    }

    [Fact]
    public async Task No_endpoint_declares_itself_anonymous_so_the_list_is_the_only_way_to_be_public()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);

        var declared = EndpointsOf(api).Where(x => x.DeclaresAnonymous).Select(x => $"{x.Methods} {x.Pattern}");

        Assert.Empty(declared);
    }

    [Fact]
    public async Task Every_entry_of_the_list_is_an_endpoint_that_exists()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var endpoints = EndpointsOf(api);

        foreach (var entry in PublicEndpoints.All)
        {
            Assert.Contains(endpoints, x => x.Pattern == entry.Pattern);
        }

        Assert.Equal(PUBLIC.Length, PublicEndpoints.All.Count);
    }

    [Fact]
    public async Task Every_endpoint_that_is_not_on_the_list_answers_401_not_signed_in_to_an_anonymous_caller()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var protectedEndpoints = EndpointsOf(api).Where(x => x.Access == "protected").ToList();

        Assert.NotEmpty(protectedEndpoints);
        foreach (var endpoint in protectedEndpoints)
        {
            foreach (var method in endpoint.MethodList)
            {
                using var request = new HttpRequestMessage(
                    new HttpMethod(method),
                    Regex.Replace(endpoint.Pattern, @"\{[^}]*\}", "x").Insert(0, "/")
                );
                using var response = await client.SendAsync(request);

                Assert.True(
                    response.StatusCode == HttpStatusCode.Unauthorized,
                    $"{method} /{endpoint.Pattern} answered {(int)response.StatusCode} to an anonymous caller"
                );
                Assert.Equal("application/vnd.api+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal("not-signed-in", await ErrorCodeAsync(response));
                Assert.Null(response.Headers.Location); // never a redirect
            }
        }
    }

    [Theory]
    [InlineData("/healthz", HttpStatusCode.OK)]
    [InlineData("/sign-in", HttpStatusCode.OK)]
    [InlineData("/register", HttpStatusCode.OK)]
    [InlineData("/privacy", HttpStatusCode.OK)]
    [InlineData("/account/assets/account.css", HttpStatusCode.OK)]
    [InlineData("/startlist", HttpStatusCode.OK)] // a deep link into the Ui
    [InlineData("/api/anything", HttpStatusCode.NotFound)] // an unknown route is a 404 for everybody
    public async Task The_public_pages_and_views_stay_open_to_an_anonymous_caller(string path, HttpStatusCode expected)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_caller_reads_without_the_header_and_changes_state_only_with_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var page = await SignedInAsync(api, client);

        var read = await page.GetAsync("/api/me");
        page.WriteHeader = null;
        var withoutHeader = await page.PostAsync("/api/passkeys/actions/creation-options");
        var rename = await page.WriteAsync(HttpMethod.Patch, "/api/passkeys/abc", "passkeys", new { name = "x" });
        var remove = await page.DeleteAsync("/api/passkeys/abc");
        page.WriteHeader = "XMLHttpRequest";
        var wrongValue = await page.DeleteAsync("/api/passkeys/abc");
        page.WriteHeader = "NoTiming";
        var withHeader = await page.DeleteAsync("/api/passkeys/abc");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        foreach (var refused in new[] { withoutHeader, rename, remove, wrongValue })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("request-header-required", await ErrorCodeAsync(refused));
        }

        Assert.Equal(HttpStatusCode.NotFound, withHeader.StatusCode); // let through: it is the route that says no such passkey
    }

    [Fact]
    public async Task A_write_that_carries_the_session_cookie_from_another_origin_is_refused_without_the_header()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var page = await SignedInAsync(api, client);
        page.WriteHeader = null;

        using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/passkeys/actions/creation-options");
        forged.Headers.Add("Origin", "https://evil.example");
        var response = await page.SendAsync(forged);
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/passkeys/actions/creation-options");
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-requested-with,content-type");
        var asked = await client.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request-header-required", await ErrorCodeAsync(response));
        // The browser would not send the header either: the other origin is not allowed to, so the preflight says no.
        Assert.False(asked.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(asked.Headers.Contains("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task The_sign_in_surface_needs_no_header_because_nobody_is_signed_in_yet()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var asked = await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("anonymous"));
        var signedOut = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/sessions/current"));

        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
    }

    async Task<PageClient> SignedInAsync(ApiFactory api, HttpClient client)
    {
        var email = UserSeed.NewEmail("baseline");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        var page = new PageClient(client);
        page.Set(await ApiSessions.SignInAsync(api, client, email));
        return page;
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    /// <summary>The routes of the host, as they are mapped and as the list sees them.</summary>
    static List<EndpointFacts> EndpointsOf(ApiFactory api)
    {
        var all = new List<EndpointFacts>();
        foreach (var source in api.Services.GetServices<EndpointDataSource>())
        {
            foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
                var pattern = endpoint.RoutePattern.RawText!.TrimStart('/');
                var asked = methods is { Count: > 0 }
                    ? methods.Order().ToList()
                    : ["GET", "POST", "PUT", "PATCH", "DELETE"];
                var accesses = asked.Select(x => PublicEndpoints.AccessOf(x, pattern)).Distinct().ToList();
                Assert.True(
                    accesses.Count == 1,
                    $"{string.Join(",", asked)} {pattern} is public for some methods only"
                );
                all.Add(
                    new EndpointFacts(
                        methods is { Count: > 0 } ? string.Join(",", asked) : "*",
                        asked,
                        pattern,
                        accesses[0] switch
                        {
                            EndpointAccess.PublicRead => "public-read",
                            EndpointAccess.SignIn => "sign-in",
                            _ => "protected",
                        },
                        endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null
                    )
                );
            }
        }

        return all;
    }

    sealed class EndpointFacts
    {
        public EndpointFacts(
            string methods,
            IReadOnlyList<string> methodList,
            string pattern,
            string access,
            bool anonymous
        )
        {
            Methods = methods;
            MethodList = methodList;
            Pattern = pattern;
            Access = access;
            DeclaresAnonymous = anonymous;
        }

        public string Methods { get; }
        public IReadOnlyList<string> MethodList { get; }
        public string Pattern { get; }
        public string Access { get; }
        public bool DeclaresAnonymous { get; }
    }
}
