using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using Not.Identity.Email;
using Not.Identity.Mongo;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The identity user store on a MongoDB container (ADR-0002): what cannot be seen over HTTP. The store works over the
/// application's existing user documents, so a row from before identity must read, update and survive field for field.
/// Every test has a database of its own.
/// </summary>
public sealed class IdentityStoreTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public IdentityStoreTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_row_from_before_identity_reads_and_updates_without_touching_a_field_of_its_own()
    {
        await using var store = await StoreAsync();
        var email = UserSeed.NewEmail("legacy");
        var original = LegacyRow(email, Guid.NewGuid());
        await store.Users.InsertOneAsync(original);

        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = await users.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed); // existing rows start with an unconfirmed address
        Assert.Null(user.SecurityStamp);
        Assert.Null(user.ConcurrencyStamp);
        Assert.Equal("Ana Petrova", user.OtherFields!["Name"].AsString); // the application's fields stay readable

        // Identity refuses to update a user without a stamp, so a row from before identity gets one with its first
        // update, as the code sign-in gives it.
        user.EmailConfirmed = true;
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        user.AccessFailedCount = 2;
        Assert.True((await users.UpdateAsync(user)).Succeeded);

        var raw = await store.Users.Find(new BsonDocument("_id", original["_id"])).SingleAsync();
        foreach (var field in original)
        {
            Assert.Equal(field.Value, raw[field.Name]); // legacy and unknown fields alike, to the last element
        }

        Assert.True(raw["EmailConfirmed"].AsBoolean);
        Assert.False(string.IsNullOrEmpty(raw["SecurityStamp"].AsString));
        Assert.False(string.IsNullOrEmpty(raw["ConcurrencyStamp"].AsString));
    }

    [Fact]
    public async Task Every_identity_field_round_trips()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var lockedUntil = new DateTimeOffset(2031, 5, 17, 9, 30, 15, 123, TimeSpan.Zero);
        var user = new NIdentityUser
        {
            Email = UserSeed.NewEmail("round-trip"),
            EmailConfirmed = true,
            LockoutEnabled = true,
            LockoutEnd = lockedUntil,
            AccessFailedCount = 3,
            ExternalProvider = "google",
            ExternalSubject = "subject-1",
        };

        Assert.True((await users.CreateAsync(user)).Succeeded);
        var loaded = await users.FindByIdAsync(user.Id.ToString());

        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded.Id);
        Assert.Equal(user.Email, loaded.Email);
        Assert.True(loaded.EmailConfirmed);
        Assert.Equal(user.SecurityStamp, loaded.SecurityStamp);
        Assert.False(string.IsNullOrEmpty(loaded.SecurityStamp));
        Assert.Equal(user.ConcurrencyStamp, loaded.ConcurrencyStamp);
        Assert.True(loaded.LockoutEnabled);
        Assert.Equal(lockedUntil, loaded.LockoutEnd);
        Assert.Equal(3, loaded.AccessFailedCount);
        Assert.Equal("google", loaded.ExternalProvider);
        Assert.Equal("subject-1", loaded.ExternalSubject);
    }

    [Fact]
    public async Task A_new_user_is_stored_with_a_standard_UUID_id()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = new NIdentityUser { Email = UserSeed.NewEmail("uuid") };

        Assert.True((await users.CreateAsync(user)).Succeeded);

        var raw = await store.Users.Find(new BsonDocument("Email", user.Email)).SingleAsync();
        Assert.Equal(BsonBinarySubType.UuidStandard, raw["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(user.Id, raw["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }

    [Theory]
    [InlineData("ana.petrova@example.test")]
    [InlineData("ANA.PETROVA@EXAMPLE.TEST")]
    [InlineData("Ana.Petrova@Example.Test")]
    [InlineData("  ana.petrova@example.test  ")]
    public async Task A_varied_capitalisation_email_resolves_to_exactly_one_account(string variant)
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = new NIdentityUser { Email = "Ana.Petrova@Example.Test" };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.Equal("ana.petrova@example.test", user.Email); // stored lower case, as the rows before identity are

        var byEmail = await users.FindByEmailAsync(variant);
        var byName = await users.FindByNameAsync(variant);
        var duplicate = await users.CreateAsync(new NIdentityUser { Email = variant });

        Assert.Equal(user.Id, byEmail!.Id);
        Assert.Equal(user.Id, byName!.Id);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(1, await store.Users.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task A_concurrent_update_is_rejected_instead_of_silently_lost()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var created = new NIdentityUser { Email = UserSeed.NewEmail("concurrent") };
        Assert.True((await users.CreateAsync(created)).Succeeded);
        var first = (await users.FindByIdAsync(created.Id.ToString()))!;
        var second = (await users.FindByIdAsync(created.Id.ToString()))!;

        first.EmailConfirmed = true;
        var firstResult = await users.UpdateAsync(first);
        second.AccessFailedCount = 7;
        var secondResult = await users.UpdateAsync(second);

        Assert.True(firstResult.Succeeded);
        Assert.False(secondResult.Succeeded);
        Assert.Equal(new IdentityErrorDescriber().ConcurrencyFailure().Code, Assert.Single(secondResult.Errors).Code);
        var stored = (await users.FindByIdAsync(created.Id.ToString()))!;
        Assert.True(stored.EmailConfirmed); // what the first one wrote is still there
        Assert.Equal(0, stored.AccessFailedCount); // and what the second one tried to write is not

        stored.AccessFailedCount = 7; // based on what is stored now, the same change goes through
        Assert.True((await users.UpdateAsync(stored)).Succeeded);
    }

    [Fact]
    public async Task Of_two_simultaneous_updates_from_the_same_state_exactly_one_succeeds()
    {
        await using var store = await StoreAsync();
        var created = new NIdentityUser { Email = UserSeed.NewEmail("race") };
        using (var scope = store.Provider.CreateScope())
        {
            Assert.True(
                (
                    await scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>().CreateAsync(created)
                ).Succeeded
            );
        }

        async Task<bool> TryUpdate(int failures)
        {
            using var scope = store.Provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
            var user = (await users.FindByIdAsync(created.Id.ToString()))!;
            await Task.Delay(50); // both have read before either writes
            user.AccessFailedCount = failures;
            return (await users.UpdateAsync(user)).Succeeded;
        }

        var results = await Task.WhenAll(TryUpdate(1), TryUpdate(2));

        Assert.Equal(1, results.Count(x => x));
    }

    [Fact]
    public async Task Rotating_the_security_stamp_changes_it_and_deletes_the_sessions_of_that_user_only()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = new NIdentityUser { Email = UserSeed.NewEmail("rotate") };
        var other = new NIdentityUser { Email = UserSeed.NewEmail("rotate-other") };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.CreateAsync(other)).Succeeded);
        var before = user.SecurityStamp;
        await store.AddSession(user.Id);
        await store.AddSession(user.Id);
        await store.AddSession(other.Id);

        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);

        Assert.NotEqual(before, user.SecurityStamp);
        Assert.Equal(
            before is null ? null : user.SecurityStamp,
            (await users.FindByIdAsync(user.Id.ToString()))!.SecurityStamp
        );
        Assert.Equal(0, await store.Sessions.CountDocumentsAsync(store.SessionsOf(user.Id)));
        Assert.Equal(1, await store.Sessions.CountDocumentsAsync(store.SessionsOf(other.Id)));
    }

    [Fact]
    public async Task Updates_that_leave_the_stamp_alone_leave_the_sessions_alone()
    {
        await using var store = await StoreAsync();
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = new NIdentityUser { Email = UserSeed.NewEmail("quiet") };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        await store.AddSession(user.Id);

        user.EmailConfirmed = true;
        Assert.True((await users.UpdateAsync(user)).Succeeded);

        Assert.Equal(1, await store.Sessions.CountDocumentsAsync(store.SessionsOf(user.Id)));
    }

    [Fact]
    public async Task The_partial_unique_indexes_allow_many_users_without_the_optional_value_and_refuse_a_duplicate()
    {
        await using var store = await StoreAsync();

        // Rows from before identity: no external login, and some have no email at all.
        for (var index = 0; index < 5; index++)
        {
            await store.Users.InsertOneAsync(
                new BsonDocument { { "_id", Binary(Guid.NewGuid()) }, { "Name", $"No email {index}" } }
            );
            await store.Users.InsertOneAsync(LegacyRow(UserSeed.NewEmail($"plain{index}"), Guid.NewGuid()));
        }

        // A provider without a subject is no login: many of those too.
        await store.Users.InsertOneAsync(
            new BsonDocument { { "_id", Binary(Guid.NewGuid()) }, { "ExternalProvider", "google" } }
        );
        await store.Users.InsertOneAsync(
            new BsonDocument { { "_id", Binary(Guid.NewGuid()) }, { "ExternalProvider", "google" } }
        );

        var login = new BsonDocument { { "ExternalProvider", "google" }, { "ExternalSubject", "subject-1" } };
        await store.Users.InsertOneAsync(new BsonDocument(login) { { "_id", Binary(Guid.NewGuid()) } });
        var sameLogin = await Assert.ThrowsAsync<MongoWriteException>(
            () => store.Users.InsertOneAsync(new BsonDocument(login) { { "_id", Binary(Guid.NewGuid()) } })
        );
        Assert.Equal(ServerErrorCategory.DuplicateKey, sameLogin.WriteError.Category);

        var email = UserSeed.NewEmail("twice");
        await store.Users.InsertOneAsync(LegacyRow(email, Guid.NewGuid()));
        var sameEmail = await Assert.ThrowsAsync<MongoWriteException>(
            () => store.Users.InsertOneAsync(LegacyRow(email, Guid.NewGuid()))
        );
        Assert.Equal(ServerErrorCategory.DuplicateKey, sameEmail.WriteError.Category);

        var indexes = (await (await store.Users.Indexes.ListAsync()).ToListAsync()).ToDictionary(x =>
            x["name"].AsString
        );
        Assert.True(indexes["identity_email_unique"]["unique"].AsBoolean);
        Assert.True(indexes["identity_email_unique"].Contains("partialFilterExpression"));
        Assert.True(indexes["identity_external_login_unique"]["unique"].AsBoolean);
        Assert.True(indexes["identity_external_login_unique"].Contains("partialFilterExpression"));
    }

    [Fact]
    public async Task Starting_again_changes_nothing_and_duplicate_emails_stop_the_host_with_a_way_out()
    {
        await using var store = await StoreAsync();
        var initializer = ActivatorUtilities.CreateInstance<IdentityIndexInitializer>(store.Provider);

        await initializer.StartAsync(CancellationToken.None); // the indexes exist: nothing to do

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(
            "duplicates_" + Guid.NewGuid().ToString("N")
        );
        var users = database.GetCollection<BsonDocument>("users");
        var email = UserSeed.NewEmail("duplicate");
        await users.InsertManyAsync([LegacyRow(email, Guid.NewGuid()), LegacyRow(email, Guid.NewGuid())]);
        var options = Options.Create(new NIdentityOptions { Database = database.DatabaseNamespace.DatabaseName });
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                new IdentityIndexInitializer(new MongoClient(_mongo.ConnectionString), options).StartAsync(
                    CancellationToken.None
                )
        );
        Assert.Contains("Resolve the duplicates", refused.Message);
    }

    [Fact]
    public async Task The_store_knows_no_tenant_the_email_is_found_whatever_the_tenant_of_the_row()
    {
        await using var store = await StoreAsync();
        var email = UserSeed.NewEmail("tenant");
        var row = LegacyRow(email, Guid.NewGuid());
        row["TenantId"] = "some-other-tenant";
        await store.Users.InsertOneAsync(row);
        using var scope = store.Provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();

        var found = await users.FindByEmailAsync(email);

        Assert.NotNull(found);
        Assert.Equal("some-other-tenant", found.OtherFields!["TenantId"].AsString);
    }

    static BsonDocument LegacyRow(string email, Guid id)
    {
        return new BsonDocument
        {
            { "_id", Binary(id) },
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
                new BsonArray { "official", "operator" }
            },
            { "TenantId", "nts" },
            {
                "UnknownToIdentity",
                new BsonDocument
                {
                    {
                        "kept",
                        new BsonArray { 1, "two", 3.5 }
                    },
                }
            },
        };
    }

    static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    async Task<StoreHarness> StoreAsync()
    {
        var database = "identity_store_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = "Development" });
        services.AddSingleton<IMongoClient>(new MongoClient(_mongo.ConnectionString));
        services.AddSingleton<IEmailSender, OutboxEmailSender>();
        services.AddNIdentity(options => options.Database = database);
        var provider = services.BuildServiceProvider();

        var initializer = provider.GetServices<IHostedService>().OfType<IdentityIndexInitializer>().Single();
        await initializer.StartAsync(CancellationToken.None);
        return new StoreHarness(provider, provider.GetRequiredService<IMongoClient>().GetDatabase(database));
    }

    sealed class StoreHarness : IAsyncDisposable
    {
        public StoreHarness(ServiceProvider provider, IMongoDatabase database)
        {
            Provider = provider;
            Users = database.GetCollection<BsonDocument>("users");
            Sessions = database.GetCollection<BsonDocument>("auth_sessions");
        }

        public ServiceProvider Provider { get; }
        public IMongoCollection<BsonDocument> Users { get; }
        public IMongoCollection<BsonDocument> Sessions { get; }

        public FilterDefinition<BsonDocument> SessionsOf(Guid userId)
        {
            return new BsonDocument("UserId", Binary(userId));
        }

        public Task AddSession(Guid userId)
        {
            return Sessions.InsertOneAsync(
                new BsonDocument
                {
                    { "_id", Guid.NewGuid().ToString("N") },
                    { "UserId", Binary(userId) },
                    { "Ticket", new BsonBinaryData(Array.Empty<byte>()) },
                    { "CreatedAt", DateTime.UtcNow },
                    { "ExpiresAt", DateTime.UtcNow.AddDays(30) },
                }
            );
        }

        public ValueTask DisposeAsync()
        {
            return Provider.DisposeAsync();
        }
    }
}
