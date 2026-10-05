using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The rows of a Tenant's registry (Clubs, Horses, Athletes) as the application stores them, written straight into the
/// collections so that a test can start from any Tenant's data. A row has the Tenant that owns it, and a member that is
/// null or default is not written, as the application does not write one.
/// </summary>
internal static class RegistrySeed
{
    public static IMongoCollection<BsonDocument> Collection(string mongoConnectionString, string name)
    {
        return new MongoClient(mongoConnectionString).GetDatabase(UserSeed.DATABASE).GetCollection<BsonDocument>(name);
    }

    public static async Task<Guid> ClubAsync(string mongoConnectionString, string tenant, string name, Guid? id = null)
    {
        var row = Row(id, tenant);
        row["Name"] = name;
        await Collection(mongoConnectionString, "clubs").InsertOneAsync(row);
        return row["_id"].AsGuid;
    }

    /// <summary>Many Clubs at once, named <c>{prefix} 0</c> to <c>{prefix} {count - 1}</c>, for a list that takes more than a page.</summary>
    public static async Task ClubsAsync(string mongoConnectionString, string tenant, int count, string prefix)
    {
        var rows = Enumerable
            .Range(0, count)
            .Select(x =>
            {
                var row = Row(null, tenant);
                row["Name"] = $"{prefix} {x}";
                return row;
            });
        await Collection(mongoConnectionString, "clubs").InsertManyAsync(rows);
    }

    public static async Task<Guid> HorseAsync(
        string mongoConnectionString,
        string tenant,
        string name,
        string? nameEnglish = null,
        string? feiId = null,
        Guid? id = null
    )
    {
        var row = Row(id, tenant);
        row["Name"] = name;
        Set(row, "NameEnglish", nameEnglish);
        Set(row, "FeiId", feiId);
        await Collection(mongoConnectionString, "horses").InsertOneAsync(row);
        return row["_id"].AsGuid;
    }

    /// <param name="country">The country as an Athlete embeds it: the fields of a country document.</param>
    public static async Task<Guid> AthleteAsync(
        string mongoConnectionString,
        string tenant,
        string name,
        BsonDocument country,
        string? nameEnglish = null,
        string? feiId = null,
        BsonDocument? club = null,
        BsonDocument? user = null,
        Guid? id = null
    )
    {
        var row = Row(id, tenant);
        row["Name"] = name;
        Set(row, "NameEnglish", nameEnglish);
        Set(row, "FeiId", feiId);
        row["Country"] = country;
        if (club != null)
        {
            row["Club"] = club;
        }

        if (user != null)
        {
            row["User"] = user;
        }

        await Collection(mongoConnectionString, "athletes").InsertOneAsync(row);
        return row["_id"].AsGuid;
    }

    /// <summary>A country as it is embedded in another document, or kept in the countries.</summary>
    public static BsonDocument CountryOf(string name, string isoCode, Guid? id = null)
    {
        return new BsonDocument
        {
            { "_id", Binary(id ?? Guid.NewGuid()) },
            { "TenantId", "nts" },
            { "Name", name },
            { "IsoCode", isoCode },
        };
    }

    public static async Task<BsonDocument?> StoredAsync(string mongoConnectionString, string collection, Guid id)
    {
        return await Collection(mongoConnectionString, collection)
            .Find(new BsonDocument("_id", Binary(id)))
            .FirstOrDefaultAsync();
    }

    public static async Task<long> CountAsync(string mongoConnectionString, string collection, string tenant)
    {
        return await Collection(mongoConnectionString, collection)
            .CountDocumentsAsync(new BsonDocument("TenantId", tenant));
    }

    public static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    static BsonDocument Row(Guid? id, string tenant)
    {
        return new BsonDocument { { "_id", Binary(id ?? Guid.NewGuid()) }, { "TenantId", tenant } };
    }

    static void Set(BsonDocument row, string member, string? value)
    {
        if (value != null)
        {
            row[member] = value;
        }
    }
}
