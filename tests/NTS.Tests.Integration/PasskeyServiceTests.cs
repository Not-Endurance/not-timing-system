using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Account;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The rules of the passkey service that no single request shows (#600): what is stored when a passkey has signed in,
/// while other requests change the same person. The service is resolved from the host, as the routes get it.
/// </summary>
public sealed class PasskeyServiceTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public PasskeyServiceTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_passkey_removed_while_it_was_signing_in_is_not_added_back()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var removed = RandomNumberGenerator.GetBytes(32);
        var kept = RandomNumberGenerator.GetBytes(32);
        var email = await SeedUserWithAsync(removed, kept);
        // What another request of the person did while the assertion was being checked.
        await Users()
            .UpdateOneAsync(
                new BsonDocument("Email", email),
                Builders<BsonDocument>.Update.PullFilter<BsonDocument>(
                    "Passkeys",
                    new BsonDocument("CredentialId", new BsonBinaryData(removed))
                )
            );
        using var scope = api.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PasskeyService>();

        var stored = await service.StoreAssertionAsync(await IdOf(email), Asserted(removed, signCount: 5));

        Assert.False(stored);
        var passkey = Assert.Single((await StoredAsync(email))["Passkeys"].AsBsonArray).AsBsonDocument;
        Assert.Equal(kept, passkey["CredentialId"].AsByteArray);
    }

    [Fact]
    public async Task A_passkey_that_signs_in_moves_its_sign_count_and_keeps_the_name_it_has_now()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var email = await SeedUserWithAsync(credentialId);
        await Users()
            .UpdateOneAsync(
                new BsonDocument("Email", email),
                Builders<BsonDocument>.Update.Set("Passkeys.0.Name", "Renamed meanwhile")
            );
        using var scope = api.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PasskeyService>();

        var stored = await service.StoreAssertionAsync(
            await IdOf(email),
            Asserted(credentialId, signCount: 7, name: "The name when the assertion began")
        );

        Assert.True(stored);
        var passkey = Assert.Single((await StoredAsync(email))["Passkeys"].AsBsonArray).AsBsonDocument;
        Assert.Equal(7, passkey["SignCount"].ToInt64());
        Assert.Equal("Renamed meanwhile", passkey["Name"].AsString);
    }

    /// <summary>What the assertion hands back for a passkey: the passkey with its sign count moved.</summary>
    static UserPasskeyInfo Asserted(byte[] credentialId, uint signCount, string? name = null)
    {
        return new UserPasskeyInfo(
            credentialId,
            [1],
            DateTimeOffset.UtcNow,
            signCount,
            [],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            [],
            []
        )
        {
            Name = name,
        };
    }

    /// <summary>A person with a security stamp (Identity will not update one without) and these passkeys stored.</summary>
    async Task<string> SeedUserWithAsync(params byte[][] passkeys)
    {
        var email = UserSeed.NewEmail("service");
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: document =>
            {
                document["SecurityStamp"] = Guid.NewGuid().ToString("N");
                document["Passkeys"] = new BsonArray(passkeys.Select(x => UserSeed.StoredPasskey(x)));
            }
        );
        return email;
    }

    async Task<Guid> IdOf(string email)
    {
        return (await StoredAsync(email))["_id"].AsGuid;
    }

    async Task<BsonDocument> StoredAsync(string email)
    {
        return await Users().Find(new BsonDocument("Email", email)).SingleAsync();
    }

    IMongoCollection<BsonDocument> Users()
    {
        return UserSeed.Users(_mongo.ConnectionString);
    }
}
