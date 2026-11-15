using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Codes;
using NC.Server.Authentication.Settings;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Stores.MongoDB.Repositories;

internal sealed class CodeChallengeRepository : ICodeChallengeRepository
{
    readonly IMongoCollection<CodeChallengeDocument> _challenges;

    public CodeChallengeRepository(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _challenges = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<CodeChallengeDocument>(settings.ChallengesCollection);
    }

    public Task<CodeChallengeDocument?> Find(string email, CancellationToken cancellationToken = default)
    {
        return _challenges.Find(x => x.Email == email).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task Replace(CodeChallengeDocument challenge, CancellationToken cancellationToken = default)
    {
        return _challenges.ReplaceOneAsync(
            x => x.Email == challenge.Email,
            challenge,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }

    public Task<CodeChallengeDocument?> SpendAttempt(
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        return _challenges.FindOneAndUpdateAsync(
            Builders<CodeChallengeDocument>.Filter.And(
                Builders<CodeChallengeDocument>.Filter.Eq(x => x.Email, email),
                Builders<CodeChallengeDocument>.Filter.Gt(x => x.ExpiresAtUtc, now.UtcDateTime),
                Builders<CodeChallengeDocument>.Filter.Gt(x => x.AttemptsLeft, 0)
            ),
            Builders<CodeChallengeDocument>.Update.Inc(x => x.AttemptsLeft, -1),
            new FindOneAndUpdateOptions<CodeChallengeDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken
        )!;
    }

    public async Task<bool> Consume(string email, byte[] mac, CancellationToken cancellationToken = default)
    {
        var result = await _challenges.DeleteOneAsync(
            Builders<CodeChallengeDocument>.Filter.And(
                Builders<CodeChallengeDocument>.Filter.Eq(x => x.Email, email),
                Builders<CodeChallengeDocument>.Filter.Eq(x => x.Mac, mac)
            ),
            cancellationToken
        );

        return result.DeletedCount == 1;
    }
}
