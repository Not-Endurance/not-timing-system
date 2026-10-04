using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>The countries the application keeps in its <c>countries</c> collection, as the Setup feature writes them.</summary>
internal static class CountrySeed
{
    public const string COLLECTION = "countries";

    /// <summary>
    /// An ISO code no other test uses, so that a test can tell its own Tenant from another's: six letters, so that two
    /// codes of one run do not meet by chance (the Tenant of a country is named by its code, and a Tenant is made once).
    /// </summary>
    public static string UniqueIsoCode()
    {
        return new string(
            Guid.NewGuid().ToString("N")[..6].Select(x => (char)('A' + Convert.ToInt32(x.ToString(), 16))).ToArray()
        );
    }

    public static IMongoCollection<BsonDocument> Countries(string mongoConnectionString)
    {
        return new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>(COLLECTION);
    }

    /// <summary>A country with no ISO code has none of the field, as the application does not write a null.</summary>
    public static async Task<Guid> AddAsync(
        string mongoConnectionString,
        string name,
        string? isoCode,
        string? nfCode = null,
        string? locale = null
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
            { "TenantId", "nts" },
            { "Name", name },
        };
        if (isoCode != null)
        {
            document["IsoCode"] = isoCode;
        }

        if (nfCode != null)
        {
            document["NfCode"] = nfCode;
        }

        if (locale != null)
        {
            document["Locale"] = locale;
        }

        await Countries(mongoConnectionString).InsertOneAsync(document);
        return id;
    }
}
