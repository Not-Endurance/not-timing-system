using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Tenants and the accounts that belong to them, as the Api keeps them (ADR-0012): a document in <c>tenants</c>, and a
/// user with a home Tenant, Memberships with their roles, and the Developer flag. Every Tenant has an id of its own, so
/// that tests that share a database do not see each other's accounts, events or roles.
/// </summary>
internal static class TenancySeed
{
    public const string TENANTS = "tenants";

    public static IMongoCollection<BsonDocument> Tenants(string mongoConnectionString)
    {
        return new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>(TENANTS);
    }

    /// <param name="withCountry">
    /// Whether the country the Tenant is of is there too, with an ISO code of its own: an Event is made in the country of its
    /// Tenant, found by the ISO code the Tenant's id holds.
    /// </param>
    public static async Task<string> TenantAsync(
        string mongoConnectionString,
        string? name = null,
        BsonDocument? rules = null,
        bool withCountry = false
    )
    {
        string id;
        string tenantName;
        if (withCountry)
        {
            var iso = CountrySeed.UniqueIsoCode();
            id = $"country-{iso.ToLowerInvariant()}";
            tenantName = name ?? "Land " + iso;
            await CountrySeed.AddAsync(mongoConnectionString, tenantName, iso);
        }
        else
        {
            id = $"country-{Guid.NewGuid():N}"[..16];
            tenantName = name ?? "Tenant " + id;
        }

        var document = new BsonDocument
        {
            { "_id", id },
            { "Name", tenantName },
            { "Kind", "country" },
        };
        if (rules != null)
        {
            document["RegionalRules"] = rules;
        }

        await Tenants(mongoConnectionString).InsertOneAsync(document);
        return id;
    }

    /// <param name="home">The home Tenant, which is also the first Membership; none for an account that has none yet.</param>
    /// <param name="roles">The roles held in a Tenant, by Tenant: a Membership is made for each named Tenant.</param>
    public static async Task<Seeded> AccountAsync(
        string mongoConnectionString,
        string? home,
        IReadOnlyDictionary<string, string[]>? roles = null,
        bool isDeveloper = false,
        string? name = null,
        string? email = null,
        string? selectedTenant = null
    )
    {
        email ??= UserSeed.NewEmail("tenancy");
        var memberships = new BsonArray();
        var seen = new HashSet<string>();
        if (home != null)
        {
            memberships.Add(Membership(home, roles?.GetValueOrDefault(home) ?? []));
            seen.Add(home);
        }

        foreach (var (tenant, held) in roles ?? new Dictionary<string, string[]>())
        {
            if (seen.Add(tenant))
            {
                memberships.Add(Membership(tenant, held));
            }
        }

        var id = await UserSeed.AddLegacyUserAsync(
            mongoConnectionString,
            email,
            shape: document =>
            {
                if (name != null)
                {
                    var parts = name.Split(' ', 2);
                    document["Name"] = name;
                    document["GivenName"] = parts[0];
                    document["Surname"] = parts.Length > 1 ? parts[1] : "Surname";
                    document.Remove("MiddleName");
                }

                if (home != null)
                {
                    document["HomeTenantId"] = home;
                }
                else
                {
                    // Signing in places a person by the country of their profile: one that is to have no Tenant names no country.
                    document.Remove("CountryRegion");
                }

                document["Memberships"] = memberships;
                if (isDeveloper)
                {
                    document["IsDeveloper"] = true;
                }

                if (selectedTenant != null)
                {
                    document["SelectedTenantId"] = selectedTenant;
                }
            }
        );
        return new Seeded(id, email);
    }

    /// <summary>The account, signed in the way a person does: a code asked for and read from the outbox.</summary>
    public static async Task<Person> SignedInAsync(
        ApiFactory api,
        HttpClient client,
        string mongoConnectionString,
        string? home,
        IReadOnlyDictionary<string, string[]>? roles = null,
        bool isDeveloper = false,
        string? name = null,
        string? selectedTenant = null
    )
    {
        var seeded = await AccountAsync(mongoConnectionString, home, roles, isDeveloper, name, null, selectedTenant);
        var page = new PageClient(client);
        page.Set(await ApiSessions.SignInAsync(api, client, seeded.Email));
        return new Person(seeded.Id, seeded.Email, page);
    }

    public static IReadOnlyDictionary<string, string[]> TenantRootOf(params string[] tenants)
    {
        return tenants.ToDictionary(x => x, _ => new[] { "tenant-root" });
    }

    static BsonDocument Membership(string tenant, string[] roles)
    {
        return new BsonDocument { { "TenantId", tenant }, { "Roles", new BsonArray(roles) } };
    }

    internal sealed class Seeded
    {
        public Seeded(Guid id, string email)
        {
            Id = id;
            Email = email;
        }

        public Guid Id { get; }
        public string Email { get; }
    }

    internal sealed class Person
    {
        public Person(Guid id, string email, PageClient page)
        {
            Id = id;
            Email = email;
            Page = page;
        }

        public Guid Id { get; }
        public string Email { get; }
        public PageClient Page { get; }
    }
}
