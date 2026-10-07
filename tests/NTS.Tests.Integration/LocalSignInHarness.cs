using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// What the tests of the local sign in as (#607) have in common, whether the build they run in has it or not: a host over the
/// database of the container with the allow-list set, a marker written as the test asks, and the requests, each from the
/// address the test names (the host of a test has none of its own, so the address is told in a header).
/// </summary>
internal static class LocalSignInHarness
{
    public const string LOOPBACK = "127.0.0.1";

    public static ApiFactory ApiOver(
        string connectionString,
        string email,
        string? second = null,
        string environment = "Development"
    )
    {
        return new ApiFactory(
            connectionString,
            environment: environment,
            configureHost: builder =>
            {
                builder.UseSetting("Dev:SignInAs:AllowList:0", email);
                if (second != null)
                {
                    builder.UseSetting("Dev:SignInAs:AllowList:1", second);
                }
            }
        );
    }

    /// <summary>The marker of the database the Api is over, written as it is asked: none removes it.</summary>
    public static async Task SetMarkerAsync(string connectionString, string? name)
    {
        var database = new MongoClient(connectionString).GetDatabase(UserSeed.DATABASE);
        await database
            .GetCollection<BsonDocument>(EnvironmentMarker.COLLECTION)
            .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        if (name != null)
        {
            await EnvironmentMarker.WriteAsync(database, name, DateTimeOffset.UtcNow);
        }
    }

    public static Task<HttpResponseMessage> SignInAsAsync(
        HttpClient client,
        string email,
        string? remote = LOOPBACK,
        string? host = null,
        string? forwarding = null
    )
    {
        return PostAsync(
            client,
            JsonSerializer.Serialize(new { data = new { type = "sessions", attributes = new { email } } }),
            remote,
            host,
            forwarding
        );
    }

    public static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string body,
        string? remote = LOOPBACK,
        string? host = null,
        string? forwarding = null
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/dev/sessions")
        {
            Content = new StringContent(body, Encoding.UTF8, ApiSessions.MEDIA_TYPE),
        };
        Address(request, remote, host, forwarding);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetPageAsync(HttpClient client, string? remote = LOOPBACK)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/dev/sign-in-as");
        Address(request, remote, null, null);
        return client.SendAsync(request);
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        return (await ApiSessions.ReadJsonAsync(response)).GetProperty("errors")[0].GetProperty("code").GetString();
    }

    /// <param name="forwarding">The name of a header a proxy puts on a request that it forwards, which the request carries.</param>
    static void Address(HttpRequestMessage request, string? remote, string? host, string? forwarding)
    {
        if (remote != null)
        {
            request.Headers.Add(ApiFactory.CLIENT_ADDRESS_HEADER, remote);
        }

        if (host != null)
        {
            request.Headers.Host = host;
        }

        if (forwarding != null)
        {
            request.Headers.TryAddWithoutValidation(forwarding, "for=203.0.113.9");
        }
    }
}
