using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using Not.Identity.Email;
using Not.Identity.Mongo;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The identity stores on a MongoDB, with a database of their own and no host around them: for what cannot be seen over
/// HTTP, such as a row from before identity or two writes that race.
/// </summary>
internal sealed class IdentityStoreHarness : IAsyncDisposable
{
    public static async Task<IdentityStoreHarness> CreateAsync(MongoFixture mongo)
    {
        var database = "identity_store_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = "Development" });
        services.AddSingleton<IMongoClient>(new MongoClient(mongo.ConnectionString));
        services.AddSingleton<IEmailSender, OutboxEmailSender>();
        services.AddNIdentity(options => options.Database = database);
        var provider = services.BuildServiceProvider();

        var initializer = provider.GetServices<IHostedService>().OfType<IdentityIndexInitializer>().Single();
        await initializer.StartAsync(CancellationToken.None);
        return new IdentityStoreHarness(provider, provider.GetRequiredService<IMongoClient>().GetDatabase(database));
    }

    /// <summary>A row of the Functions API with the profile fields, and one field no code of ours knows.</summary>
    public static BsonDocument LegacyRow(string email, Guid id)
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

    public static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    IdentityStoreHarness(ServiceProvider provider, IMongoDatabase database)
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
