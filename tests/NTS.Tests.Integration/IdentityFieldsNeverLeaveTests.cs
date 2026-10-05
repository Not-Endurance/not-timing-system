using System.Net;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What an account keeps to sign in with (its security stamp, its passkeys, its lockout data) never leaves in any response
/// (#603, ADR-0002, ADR-0012). The routes that stand where the legacy <c>GET users</c> stood, the search of accounts by name,
/// answer with a name and a masked email and nothing else, and the rest of the routes of reference data and Setups show the
/// people they mention (a rider linked to an account, an Official or an Operator named by an email) as the Setup knows them
/// and never as the account is kept. One account with every identity field set and recognisable is put in each place such a
/// route could show it from, and every route that could is read as the people who may.
/// </summary>
public sealed class IdentityFieldsNeverLeaveTests : IClassFixture<MongoFixture>
{
    static readonly string[] SECRET_NAMES =
    [
        "securityStamp",
        "SecurityStamp",
        "passkeys",
        "Passkeys",
        "credentialId",
        "CredentialId",
        "lockoutEnd",
        "LockoutEnd",
        "accessFailedCount",
        "AccessFailedCount",
        "passwordHash",
        "PasswordHash",
        "normalizedEmail",
        "NormalizedEmail",
    ];

    readonly MongoFixture _mongo;

    public IdentityFieldsNeverLeaveTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task No_route_that_stands_where_the_user_routes_stood_or_shows_a_person_gives_what_the_account_signs_in_with()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var stamp = "STAMP-" + Guid.NewGuid().ToString("N");
        var credential = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 11, 12 };
        var person = await AccountWithEverythingAsync(tenant, marker, stamp, credential);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Rider {marker}",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            user: new BsonDocument
            {
                { "_id", RegistrySeed.Binary(person.Id) },
                { "TenantId", "nts" },
                { "Email", person.Email },
                { "Name", $"Marker {marker}" },
            }
        );
        var eventId = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, $"Event {marker}");
        await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, eventId, "Operator", null, person.Email, person.Id);
        var secrets = new[] { stamp, Convert.ToBase64String(credential), Base64Url(credential) };

        var routes = new[]
        {
            $"/api/accounts?filter=contains(name,'marker {marker}')",
            $"/api/accounts/all-tenants?filter=contains(name,'marker {marker}')",
            "/api/athletes",
            $"/api/athletes/{athlete}",
            $"/api/athletes/all-tenants?filter=contains(name,'{marker}')",
            "/api/clubs",
            "/api/horses",
            "/api/countries",
            "/api/configure-events",
            $"/api/configure-events/{eventId}",
            $"/api/event-grants?filter=eventId eq {eventId}",
            $"/api/events/{eventId}/capabilities",
            $"/api/tenants/{tenant}",
        };
        foreach (var route in routes)
        {
            // The grants of an Event are the Main Operator's to see; every other route here is the Tenant Root's.
            var reader = route.StartsWith("/api/event-grants", StringComparison.Ordinal) ? mainOperator : root;
            var response = await reader.Page.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route} answered {response.StatusCode}");
            foreach (var secret in secrets)
            {
                Assert.False(
                    body.Contains(secret, StringComparison.Ordinal),
                    $"{route} gives a value an account signs in with"
                );
            }

            foreach (var name in SECRET_NAMES)
            {
                Assert.False(body.Contains($"\"{name}\"", StringComparison.Ordinal), $"{route} names {name}");
            }
        }

        var found = await root.Page.GetAsync($"/api/accounts?filter=contains(name,'marker {marker}')");
        Assert.Single((await ApiSessions.ReadJsonAsync(found)).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task The_legacy_routes_that_listed_users_and_read_one_by_email_are_not_in_the_Api()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        foreach (
            var route in new[]
            {
                "/api/users",
                $"/api/users/{root.Email}",
                $"/api/users/{Uri.EscapeDataString(root.Email)}",
            }
        )
        {
            var response = await root.Page.GetAsync(route);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    async Task<(Guid Id, string Email)> AccountWithEverythingAsync(
        string tenant,
        string marker,
        string stamp,
        byte[] credential
    )
    {
        var email = UserSeed.NewEmail("secret");
        var id = await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: document =>
            {
                document["Name"] = $"Marker {marker}";
                document["HomeTenantId"] = tenant;
                document["Memberships"] = new BsonArray
                {
                    new BsonDocument { { "TenantId", tenant }, { "Roles", new BsonArray() } },
                };
                document["EmailConfirmed"] = true;
                document["SecurityStamp"] = stamp;
                document["LockoutEnd"] = new BsonDateTime(DateTime.UtcNow.AddDays(1));
                document["AccessFailedCount"] = 3;
                document["Passkeys"] = new BsonArray { UserSeed.StoredPasskey(credential, "Secret passkey") };
            }
        );
        return (id, email);
    }

    static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
