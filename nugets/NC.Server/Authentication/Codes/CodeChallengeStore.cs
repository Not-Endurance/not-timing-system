using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Store;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Codes;

public sealed class CodeChallengeStore
{
    public const string SIGN_IN = "sign-in";
    public const string REGISTER = "register";

    readonly IMongoCollection<CodeChallengeDocument> _challenges;

    public CodeChallengeStore(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _challenges = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<CodeChallengeDocument>(settings.ChallengesCollection);
    }

    public Task<CodeChallengeDocument?> FindAsync(string email, CancellationToken cancellationToken = default)
    {
        return _challenges.Find(x => x.Email == email).FirstOrDefaultAsync(cancellationToken)!;
    }

    /// <summary>A new request replaces the previous code, so only the latest code works.</summary>
    public Task ReplaceAsync(CodeChallengeDocument challenge, CancellationToken cancellationToken = default)
    {
        return _challenges.ReplaceOneAsync(
            x => x.Email == challenge.Email,
            challenge,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }

    /// <summary>
    /// Uses up one attempt before the code is compared, and returns the challenge with the attempt spent. Null when the
    /// challenge is missing, expired or out of attempts.
    /// </summary>
    public Task<CodeChallengeDocument?> SpendAttemptAsync(
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        return _challenges.FindOneAndUpdateAsync(
            Builders<CodeChallengeDocument>.Filter.And(
                Builders<CodeChallengeDocument>.Filter.Eq(x => x.Email, email),
                Builders<CodeChallengeDocument>.Filter.Gt(x => x.ExpiresAt, now.UtcDateTime),
                Builders<CodeChallengeDocument>.Filter.Gt(x => x.AttemptsLeft, 0)
            ),
            Builders<CodeChallengeDocument>.Update.Inc(x => x.AttemptsLeft, -1),
            new FindOneAndUpdateOptions<CodeChallengeDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken
        )!;
    }

    /// <summary>Deletes the challenge only while it still holds this MAC, so a code is used once even under a race.</summary>
    public async Task<bool> ConsumeAsync(string email, byte[] mac, CancellationToken cancellationToken = default)
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
