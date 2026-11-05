using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NoTiming.IntegrationTests.Authentication.Harness;

/// <summary>Seeds the rows a scenario starts from, and reads the code out of the outbox the way a person would read their mail.</summary>
internal sealed class MongoAccess
{
    readonly IMongoDatabase _database;

    public MongoAccess(string connectionString)
    {
        _database = new MongoClient(connectionString).GetDatabase("nts");
    }

    /// <summary>A user row as it was before identity: an integer id, roles, and possibly no proof of its address.</summary>
    public Task SeedUserAsync(int id, string email, bool proved, string[] roles)
    {
        var document = new BsonDocument
        {
            { "_id", id },
            { "Email", email },
            { "Name", "Seeded Person" },
            { "Roles", new BsonArray(roles) },
            { "TenantId", "nts" },
        };
        if (proved)
        {
            document.Add("ProvedAt", DateTime.UtcNow);
            document.Add("SecurityStamp", $"seed-{id}");
        }

        return _database.GetCollection<BsonDocument>("users").InsertOneAsync(document);
    }

    /// <summary>
    /// A user row as it was before identity, with each profile field exactly as given (absent fields stay absent). Sign-in
    /// adopts such a row when its address is proved, keeping its id and profile.
    /// </summary>
    public Task SeedLegacyUserAsync(int id, string email, string? name, string? givenName, string? middleName, string? surname, string? countryRegion, string? club, string? feiId, string? displayName)
    {
        var document = new BsonDocument
        {
            { "_id", id },
            { "Email", email },
            { "Name", BsonValueOrNull(name) },
            { "GivenName", BsonValueOrNull(givenName) },
            { "MiddleName", BsonValueOrNull(middleName) },
            { "Surname", BsonValueOrNull(surname) },
            { "CountryRegion", BsonValueOrNull(countryRegion) },
            { "Club", BsonValueOrNull(club) },
            { "FeiId", BsonValueOrNull(feiId) },
            { "DisplayName", BsonValueOrNull(displayName) },
            { "Roles", new BsonArray() },
            { "TenantId", "nts" },
        };

        return _database.GetCollection<BsonDocument>("users").InsertOneAsync(document);
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        var count = await _database
            .GetCollection<BsonDocument>("users")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Email", email));
        return count > 0;
    }

    public async Task<int> UserIdByEmailAsync(string email)
    {
        var user = await _database
            .GetCollection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.Eq("Email", email))
            .FirstAsync();
        return user["_id"].AsInt32;
    }

    /// <summary>Gives a user passkeys as the handler stores them. The keys are not real; the store does not check them.</summary>
    public Task SeedPasskeysAsync(int userId, params (byte[] CredentialId, string Name)[] passkeys)
    {
        var documents = passkeys.Select(passkey => new BsonDocument
        {
            { "CredentialId", new BsonBinaryData(passkey.CredentialId) },
            { "PublicKey", new BsonBinaryData(new byte[] { 1, 2, 3 }) },
            { "Name", passkey.Name },
            { "CreatedAt", DateTime.UtcNow },
            { "SignCount", 0L },
            { "Transports", new BsonArray() },
            { "IsUserVerified", true },
            { "IsBackupEligible", false },
            { "IsBackedUp", false },
            { "AttestationObject", new BsonBinaryData(Array.Empty<byte>()) },
            { "ClientDataJson", new BsonBinaryData(Array.Empty<byte>()) },
        });

        return _database
            .GetCollection<BsonDocument>("users")
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", userId),
                Builders<BsonDocument>.Update.Set("Passkeys", new BsonArray(documents))
            );
    }

    public Task<BsonDocument?> ReadUserAsync(int id)
    {
        return _database.GetCollection<BsonDocument>("users").Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync()!;
    }

    public async Task<int> CountOutboxAsync(string email)
    {
        var count = await _database
            .GetCollection<BsonDocument>("email_outbox")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("To", email));
        return (int)count;
    }

    /// <summary>Waits for the newest message to the address and returns the six digits in it.</summary>
    public async Task<string> WaitForCodeAsync(string email, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var message = await _database
                .GetCollection<BsonDocument>("email_outbox")
                .Find(Builders<BsonDocument>.Filter.Eq("To", email))
                .Sort(Builders<BsonDocument>.Sort.Descending("SentAtUtc"))
                .FirstOrDefaultAsync();

            if (message != null)
            {
                var match = Regex.Match(message["TextBody"].AsString, @"\b\d{6}\b");
                if (match.Success)
                {
                    return match.Value;
                }
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No code reached the outbox for {email}.");
    }

    static BsonValue BsonValueOrNull(string? value)
    {
        return value is null ? BsonNull.Value : new BsonString(value);
    }
}
