using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using NC.Server.Authentication.Profiles;
using NC.Server.Authentication.Settings;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Stores.MongoDB.Migration;

/// <summary>
/// Moves the profile fields that users had at the top level of their row into the <c>profile</c> document. It runs at every
/// start, and a row without the old fields is not touched, so a second run changes nothing.
/// </summary>
internal sealed class UserProfileMigration : IHostedService
{
    readonly IMongoContext _context;
    readonly IOptions<AuthenticationSettings> _options;
    readonly ILogger<UserProfileMigration> _logger;

    public UserProfileMigration(
        IMongoContext context,
        IOptions<AuthenticationSettings> options,
        ILogger<UserProfileMigration> logger
    )
    {
        _context = context;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = _options.Value;
        var users = _context
            .Client.GetDatabase(settings.Database)
            .GetCollection<BsonDocument>(settings.UsersCollection);
        var legacy = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("Club"),
            Builders<BsonDocument>.Filter.Exists("FeiId"),
            Builders<BsonDocument>.Filter.Exists("CountryRegion")
        );

        var rows = await users.Find(legacy).ToListAsync(cancellationToken);
        var moved = 0;
        foreach (var row in rows)
        {
            var migrated = LegacyProfileMigration.Migrate(row);
            if (migrated is null)
            {
                continue;
            }

            await users.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", row["_id"]),
                migrated,
                cancellationToken: cancellationToken
            );
            moved++;
        }

        if (moved > 0)
        {
            _logger.LogInformation("Moved the profile of {Count} users into their profile document.", moved);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
