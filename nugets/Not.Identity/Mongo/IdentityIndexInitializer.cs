using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity.Codes;
using Not.Identity.Sessions;

namespace Not.Identity.Mongo;

/// <summary>
/// Creates the indexes identity depends on when the host starts. The unique indexes on optional fields are partial,
/// because a plain unique index treats a missing value as a value and would refuse the second user without one
/// (ADR-0002). Creating an index that already exists with the same definition does nothing.
/// </summary>
public sealed class IdentityIndexInitializer : IHostedService
{
    readonly IMongoClient _client;
    readonly NIdentityOptions _options;

    public IdentityIndexInitializer(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var database = _client.GetDatabase(_options.Database);
        await EnsureUserIndexes(database.GetCollection<NIdentityUser>(_options.UsersCollection), cancellationToken);
        await EnsureSessionIndexes(
            database.GetCollection<SessionDocument>(_options.SessionsCollection),
            cancellationToken
        );
        await EnsureChallengeIndexes(
            database.GetCollection<CodeChallenge>(_options.ChallengesCollection),
            cancellationToken
        );
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    async Task EnsureUserIndexes(IMongoCollection<NIdentityUser> users, CancellationToken cancellationToken)
    {
        try
        {
            await users.Indexes.CreateOneAsync(
                new CreateIndexModel<NIdentityUser>(
                    Builders<NIdentityUser>.IndexKeys.Ascending(x => x.Email),
                    new CreateIndexOptions<NIdentityUser>
                    {
                        Name = "identity_email_unique",
                        Unique = true,
                        PartialFilterExpression = Builders<NIdentityUser>.Filter.Type(x => x.Email, BsonType.String),
                    }
                ),
                cancellationToken: cancellationToken
            );
        }
        catch (MongoCommandException ex) when (ex.Code is 11000 or 85 or 86)
        {
            throw new InvalidOperationException(
                $"The unique index on the email of '{_options.UsersCollection}' cannot be created. Two users probably have the same email, and one person cannot be told from the other. Resolve the duplicates (the dry run of the migration lists them) and start again.",
                ex
            );
        }

        await users.Indexes.CreateOneAsync(
            new CreateIndexModel<NIdentityUser>(
                Builders<NIdentityUser>.IndexKeys.Ascending(x => x.ExternalProvider).Ascending(x => x.ExternalSubject),
                new CreateIndexOptions<NIdentityUser>
                {
                    Name = "identity_external_login_unique",
                    Unique = true,
                    PartialFilterExpression = Builders<NIdentityUser>.Filter.Type(
                        x => x.ExternalSubject,
                        BsonType.String
                    ),
                }
            ),
            cancellationToken: cancellationToken
        );

        // A credential id belongs to one user. Partial, because a user without a passkey has no field to index.
        await users.Indexes.CreateOneAsync(
            new CreateIndexModel<NIdentityUser>(
                Builders<NIdentityUser>.IndexKeys.Ascending("Passkeys.CredentialId"),
                new CreateIndexOptions<NIdentityUser>
                {
                    Name = "identity_passkey_credential_unique",
                    Unique = true,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<NIdentityUser>(
                        new BsonDocument("Passkeys.CredentialId", new BsonDocument("$exists", true))
                    ),
                }
            ),
            cancellationToken: cancellationToken
        );
    }

    static async Task EnsureSessionIndexes(
        IMongoCollection<SessionDocument> sessions,
        CancellationToken cancellationToken
    )
    {
        await sessions.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<SessionDocument>(
                    Builders<SessionDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
                    new CreateIndexOptions { Name = "sessions_expiry", ExpireAfter = TimeSpan.Zero }
                ),
                new CreateIndexModel<SessionDocument>(
                    Builders<SessionDocument>.IndexKeys.Ascending(x => x.UserId),
                    new CreateIndexOptions { Name = "sessions_user" }
                ),
            ],
            cancellationToken
        );
    }

    static async Task EnsureChallengeIndexes(
        IMongoCollection<CodeChallenge> challenges,
        CancellationToken cancellationToken
    )
    {
        await challenges.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<CodeChallenge>(
                    Builders<CodeChallenge>.IndexKeys.Ascending(x => x.ExpiresAt),
                    new CreateIndexOptions { Name = "challenges_expiry", ExpireAfter = TimeSpan.Zero }
                ),
                new CreateIndexModel<CodeChallenge>(
                    Builders<CodeChallenge>.IndexKeys.Ascending(x => x.Email).Ascending(x => x.Purpose),
                    new CreateIndexOptions { Name = "challenges_email_purpose_unique", Unique = true }
                ),
            ],
            cancellationToken
        );
    }
}
