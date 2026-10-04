using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>What the tests of the sign-in and registration routes share: a client of the host as a person has one.</summary>
internal static class ApiClients
{
    /// <summary>
    /// Https, so that the Secure session cookie is one a browser would take; the cookie itself is carried by hand.
    /// </summary>
    public static HttpClient Of(ApiFactory api)
    {
        return api.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
    }

    /// <summary>
    /// A browser's cookie jar as well: what the host sets, such as the cookie that ties a registration to the browser
    /// that made it, is sent back with the next request. <see cref="Of"/> carries nothing, so a test can say which
    /// cookie goes with which request.
    /// </summary>
    public static HttpClient OfBrowser(ApiFactory api)
    {
        return api.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
    }

    public static async Task<string> PageAsync(HttpClient client, string path, string? acceptLanguage = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptLanguage != null)
        {
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        }

        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<BsonDocument?> FindUserAsync(string mongoConnectionString, string email)
    {
        return await UserSeed.Users(mongoConnectionString).Find(new BsonDocument("Email", email)).FirstOrDefaultAsync();
    }

    public static async Task<BsonDocument?> FindTenantAsync(string mongoConnectionString, string id)
    {
        return await new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("tenants")
            .Find(new BsonDocument("_id", id))
            .FirstOrDefaultAsync();
    }

    public static async Task<long> CountTenantsAsync(string mongoConnectionString, string id)
    {
        return await new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("tenants")
            .CountDocumentsAsync(new BsonDocument("_id", id));
    }
}
