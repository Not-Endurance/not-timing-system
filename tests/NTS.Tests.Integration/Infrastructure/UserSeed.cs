using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Users as they exist before identity: a row of the Functions API with the profile fields and none of the identity
/// ones (ADR-0002). A test that signs in starts from one of these, as every existing user does.
/// </summary>
internal static class UserSeed
{
    public const string DATABASE = "nts";
    public const string COLLECTION = "users";

    public static string NewEmail(string prefix = "user")
    {
        return $"{prefix}.{Guid.NewGuid():N}@sign-in.test";
    }

    public static IMongoCollection<BsonDocument> Users(string mongoConnectionString)
    {
        return new MongoClient(mongoConnectionString).GetDatabase(DATABASE).GetCollection<BsonDocument>(COLLECTION);
    }

    public static async Task<Guid> AddLegacyUserAsync(
        string mongoConnectionString,
        string email,
        string tenantId = "nts",
        Action<BsonDocument>? shape = null
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
            { "Email", email },
            { "Name", "Ana Petrova" },
            { "DisplayName", "Ana" },
            { "GivenName", "Ana" },
            { "Surname", "Petrova" },
            { "MiddleName", "K." },
            { "CountryRegion", "Bulgaria" },
            { "Club", "Rider Club" },
            { "FeiId", "10012345" },
            {
                "Roles",
                new BsonArray { "official" }
            },
            { "TenantId", tenantId },
        };
        shape?.Invoke(document);
        await Users(mongoConnectionString).InsertOneAsync(document);
        return id;
    }
}
