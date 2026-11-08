using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Codes;
using NC.Server.Authentication.RateLimits;
using NC.Server.Authentication.Sessions;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Store;

/// <summary>
/// Creates the indexes sign-in depends on when the host starts. Uniqueness on the address applies only to proved users,
/// so legacy duplicates stay allowed until they are proved, and the expiry indexes remove sessions, challenges and
/// counters once they have passed. Creating an index that already exists with the same definition does nothing.
/// </summary>
public sealed class AuthenticationIndexes : IHostedService
{
    readonly IMongoContext _context;
    readonly AuthenticationSettings _settings;

    public AuthenticationIndexes(IMongoContext context, IOptions<AuthenticationSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var database = _context.Client.GetDatabase(_settings.Database);

        var users = database.GetCollection<IdentityUserDocument>(_settings.UsersCollection);
        await users.Indexes.CreateManyAsync(
            [
                // Default collation: the exact match MongoUiReceiveAccessPolicy uses. Sign-in uses identity_email_ci.
                new CreateIndexModel<IdentityUserDocument>(
                    Builders<IdentityUserDocument>.IndexKeys.Ascending(x => x.Email),
                    new CreateIndexOptions<IdentityUserDocument> { Name = "identity_email" }
                ),
                new CreateIndexModel<IdentityUserDocument>(
                    Builders<IdentityUserDocument>.IndexKeys.Ascending(x => x.Email),
                    new CreateIndexOptions<IdentityUserDocument>
                    {
                        Name = "identity_email_ci",
                        Collation = EmailCollation.Value,
                    }
                ),
                new CreateIndexModel<IdentityUserDocument>(
                    Builders<IdentityUserDocument>.IndexKeys.Ascending(x => x.Email),
                    new CreateIndexOptions<IdentityUserDocument>
                    {
                        Name = "identity_email_proved_unique",
                        Unique = true,
                        PartialFilterExpression = Builders<IdentityUserDocument>.Filter.Exists(x => x.ProvedAt),
                    }
                ),
                new CreateIndexModel<IdentityUserDocument>(
                    Builders<IdentityUserDocument>.IndexKeys.Ascending(x => x.UserHandle),
                    new CreateIndexOptions<IdentityUserDocument>
                    {
                        Name = "identity_user_handle_unique",
                        Unique = true,
                        PartialFilterExpression = Builders<IdentityUserDocument>.Filter.Exists(x => x.UserHandle),
                    }
                ),
            ],
            cancellationToken: cancellationToken
        );

        await database
            .GetCollection<SessionDocument>(_settings.SessionsCollection)
            .Indexes.CreateOneAsync(
                ExpiresAtIndex<SessionDocument>("sessions_expire_at"),
                cancellationToken: cancellationToken
            );
        await database
            .GetCollection<CodeChallengeDocument>(_settings.ChallengesCollection)
            .Indexes.CreateOneAsync(
                ExpiresAtIndex<CodeChallengeDocument>("challenges_expire_at"),
                cancellationToken: cancellationToken
            );
        await database
            .GetCollection<RateCounterDocument>(_settings.RateCountersCollection)
            .Indexes.CreateOneAsync(
                ExpiresAtIndex<RateCounterDocument>("rate_counters_expire_at"),
                cancellationToken: cancellationToken
            );
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    static CreateIndexModel<T> ExpiresAtIndex<T>(string name)
    {
        var keys = Builders<T>.IndexKeys.Ascending("ExpiresAt");
        return new CreateIndexModel<T>(keys, new CreateIndexOptions { Name = name, ExpireAfter = TimeSpan.Zero });
    }
}
