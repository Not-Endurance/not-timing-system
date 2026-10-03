using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.IdentityStoreHarness;

namespace NTS.Tests.Integration;

/// <summary>
/// The passkeys of a user on a MongoDB (#600, ADR-0002): embedded in the user document with binary ids and keys,
/// replaced in place each time one signs in, behind a partial unique index on the credential id. These are the store
/// scenarios of the go/no-go report; what a browser does with them is in <c>PasskeyCeremonyTests</c>.
/// </summary>
public sealed class PasskeyStoreTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public PasskeyStoreTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_passkey_with_every_field_persists_and_reloads_field_by_field_equal()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = await CreateUser(users);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var expected = new UserPasskeyInfo(
            credentialId,
            RandomNumberGenerator.GetBytes(77),
            new DateTimeOffset(2030, 6, 1, 12, 30, 45, 123, TimeSpan.Zero),
            4_000_000_001u, // past int.MaxValue: a sign count is unsigned
            ["internal", "hybrid", "usb"],
            isUserVerified: true,
            isBackupEligible: true,
            isBackedUp: false,
            attestationObject: RandomNumberGenerator.GetBytes(120),
            clientDataJson: RandomNumberGenerator.GetBytes(90)
        )
        {
            Name = "Work laptop",
        };

        Assert.True((await users.AddOrUpdatePasskeyAsync(user, expected)).Succeeded);

        var reloaded = (await users.FindByIdAsync(user.Id.ToString()))!;
        var passkey = Assert.Single(await users.GetPasskeysAsync(reloaded));
        Assert.Equal(expected.CredentialId, passkey.CredentialId);
        Assert.Equal(expected.PublicKey, passkey.PublicKey);
        Assert.Equal(expected.CreatedAt, passkey.CreatedAt);
        Assert.Equal(expected.SignCount, passkey.SignCount);
        Assert.Equal(expected.Transports, passkey.Transports);
        Assert.Equal(expected.IsUserVerified, passkey.IsUserVerified);
        Assert.Equal(expected.IsBackupEligible, passkey.IsBackupEligible);
        Assert.Equal(expected.IsBackedUp, passkey.IsBackedUp);
        Assert.Equal(expected.AttestationObject, passkey.AttestationObject);
        Assert.Equal(expected.ClientDataJson, passkey.ClientDataJson);
        Assert.Equal(expected.Name, passkey.Name);

        var byId = await users.FindByPasskeyIdAsync(credentialId);
        Assert.Equal(user.Id, byId!.Id);

        // Stored as binary, the id and the key alike.
        var raw = await store.Users.Find(Is(user.Id)).SingleAsync();
        var stored = raw["Passkeys"].AsBsonArray.Single().AsBsonDocument;
        Assert.Equal(BsonType.Binary, stored["CredentialId"].BsonType);
        Assert.Equal(BsonType.Binary, stored["PublicKey"].BsonType);
    }

    [Fact]
    public async Task Signing_in_three_times_in_a_row_leaves_exactly_one_entry_for_the_credential()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = await CreateUser(users);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        Assert.True((await users.AddOrUpdatePasskeyAsync(user, NewPasskey(credentialId, signCount: 0))).Succeeded);

        for (uint signIn = 1; signIn <= 3; signIn++)
        {
            var loaded = (await users.FindByIdAsync(user.Id.ToString()))!;
            var current = (await users.GetPasskeyAsync(loaded, credentialId))!;
            current.SignCount = signIn * 10; // what an assertion does: the credential moves, it is not added again
            Assert.True((await users.AddOrUpdatePasskeyAsync(loaded, current)).Succeeded);
        }

        var stored = (await users.FindByIdAsync(user.Id.ToString()))!;
        var passkey = Assert.Single(await users.GetPasskeysAsync(stored));
        Assert.Equal(30u, passkey.SignCount);
        Assert.Equal(1, (await store.Users.Find(Is(user.Id)).SingleAsync())["Passkeys"].AsBsonArray.Count);
    }

    [Fact]
    public async Task Passkeys_enrolled_at_the_same_moment_all_persist()
    {
        await using var store = await StoreAsync();
        Guid userId;
        using (var scope = store.Provider.CreateScope())
        {
            userId = (await CreateUser(scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>())).Id;
        }

        var credentials = Enumerable.Range(0, 3).Select(_ => RandomNumberGenerator.GetBytes(32)).ToArray();

        async Task<IdentityResult> Enrol(byte[] credentialId)
        {
            using var scope = store.Provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
            await Task.Delay(30); // all of them have started before any has finished
            return await users.ChangeAsync(
                userId,
                user => users.AddOrUpdatePasskeyAsync(user, NewPasskey(credentialId))
            );
        }

        var results = await Task.WhenAll(credentials.Select(Enrol));

        Assert.All(results, result => Assert.True(result.Succeeded));
        using var check = store.Provider.CreateScope();
        var checkUsers = check.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var stored = await checkUsers.GetPasskeysAsync((await checkUsers.FindByIdAsync(userId.ToString()))!);
        Assert.Equal(
            credentials.Select(Convert.ToBase64String).Order(),
            stored.Select(x => Convert.ToBase64String(x.CredentialId)).Order()
        );
    }

    [Fact]
    public async Task A_change_that_raced_is_refused_when_it_is_not_retried()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var created = await CreateUser(users);
        var first = (await users.FindByIdAsync(created.Id.ToString()))!;
        var second = (await users.FindByIdAsync(created.Id.ToString()))!;

        Assert.True(
            (await users.AddOrUpdatePasskeyAsync(first, NewPasskey(RandomNumberGenerator.GetBytes(32)))).Succeeded
        );
        var refused = await users.AddOrUpdatePasskeyAsync(second, NewPasskey(RandomNumberGenerator.GetBytes(32)));

        Assert.False(refused.Succeeded); // refused, not silently lost: the caller starts again from what is stored
        Assert.Single(await users.GetPasskeysAsync((await users.FindByIdAsync(created.Id.ToString()))!));
    }

    [Fact]
    public async Task A_removed_passkey_is_gone_and_can_no_longer_be_found()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = await CreateUser(users);
        var kept = RandomNumberGenerator.GetBytes(32);
        var removed = RandomNumberGenerator.GetBytes(32);
        Assert.True((await users.AddOrUpdatePasskeyAsync(user, NewPasskey(kept))).Succeeded);
        Assert.True((await users.AddOrUpdatePasskeyAsync(user, NewPasskey(removed))).Succeeded);

        Assert.True((await users.RemovePasskeyAsync(user, removed)).Succeeded);

        Assert.Null(await users.FindByPasskeyIdAsync(removed));
        Assert.Equal(user.Id, (await users.FindByPasskeyIdAsync(kept))!.Id);
        var stored = (await users.FindByIdAsync(user.Id.ToString()))!;
        Assert.Equal(kept, Assert.Single(await users.GetPasskeysAsync(stored)).CredentialId);
    }

    [Fact]
    public async Task Removing_the_last_passkey_leaves_no_field_to_index()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = await CreateUser(users);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        Assert.True((await users.AddOrUpdatePasskeyAsync(user, NewPasskey(credentialId))).Succeeded);

        Assert.True((await users.RemovePasskeyAsync(user, credentialId)).Succeeded);

        Assert.False((await store.Users.Find(Is(user.Id)).SingleAsync()).Contains("Passkeys"));
    }

    [Fact]
    public async Task A_credential_id_belongs_to_one_user_and_a_user_without_a_passkey_does_not_count()
    {
        await using var store = await StoreAsync();
        var credentialId = new BsonBinaryData(RandomNumberGenerator.GetBytes(32));

        // Many users without a passkey, with no field or with an empty one: none of them is in the index.
        for (var index = 0; index < 5; index++)
        {
            await store.Users.InsertOneAsync(LegacyRow(UserSeed.NewEmail($"plain{index}"), Guid.NewGuid()));
            var empty = LegacyRow(UserSeed.NewEmail($"empty{index}"), Guid.NewGuid());
            empty["Passkeys"] = new BsonArray();
            await store.Users.InsertOneAsync(empty);
        }

        var first = LegacyRow(UserSeed.NewEmail("first"), Guid.NewGuid());
        first["Passkeys"] = new BsonArray { new BsonDocument("CredentialId", credentialId) };
        await store.Users.InsertOneAsync(first);
        var second = LegacyRow(UserSeed.NewEmail("second"), Guid.NewGuid());
        second["Passkeys"] = new BsonArray { new BsonDocument("CredentialId", credentialId) };

        var duplicate = await Assert.ThrowsAsync<MongoWriteException>(() => store.Users.InsertOneAsync(second));

        Assert.Equal(ServerErrorCategory.DuplicateKey, duplicate.WriteError.Category);
        var credentialIndex = (await (await store.Users.Indexes.ListAsync()).ToListAsync()).Single(x =>
            x["name"] == "identity_passkey_credential_unique"
        );
        Assert.True(credentialIndex["unique"].AsBoolean);
        Assert.True(credentialIndex.Contains("partialFilterExpression"));
    }

    [Fact]
    public async Task Adding_a_passkey_leaves_every_field_of_the_application_alone()
    {
        await using var store = await StoreAsync();
        var email = UserSeed.NewEmail("fields");
        var id = Guid.NewGuid();
        var original = LegacyRow(email, id);
        await store.Users.InsertOneAsync(original);
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);

        Assert.True(
            (await users.AddOrUpdatePasskeyAsync(user, NewPasskey(RandomNumberGenerator.GetBytes(32)))).Succeeded
        );

        var raw = await store.Users.Find(Is(id)).SingleAsync();
        foreach (var field in original)
        {
            Assert.Equal(field.Value, raw[field.Name]);
        }

        Assert.Single(raw["Passkeys"].AsBsonArray);
    }

    static async Task<NIdentityUser> CreateUser(UserManager<NIdentityUser> users)
    {
        var user = new NIdentityUser { Email = UserSeed.NewEmail("passkeys") };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        return user;
    }

    static UserPasskeyInfo NewPasskey(byte[] credentialId, uint signCount = 0)
    {
        return new UserPasskeyInfo(
            credentialId,
            RandomNumberGenerator.GetBytes(77),
            DateTimeOffset.UtcNow,
            signCount,
            ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: RandomNumberGenerator.GetBytes(40),
            clientDataJson: RandomNumberGenerator.GetBytes(40)
        )
        {
            Name = "Test passkey",
        };
    }

    static BsonDocument Is(Guid id)
    {
        return new BsonDocument("_id", Binary(id));
    }

    Task<IdentityStoreHarness> StoreAsync()
    {
        return IdentityStoreHarness.CreateAsync(_mongo);
    }
}
