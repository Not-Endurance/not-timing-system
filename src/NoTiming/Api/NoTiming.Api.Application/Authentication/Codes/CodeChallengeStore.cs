using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.Codes;

/// <summary>The details a registration keeps until its code is entered, or until the challenge expires.</summary>
[BsonIgnoreExtraElements]
public sealed class RegistrationDetailsDocument
{
    public string GivenName { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string CountryRegion { get; set; } = string.Empty;
}

/// <summary>One challenge per normalized address. It holds the MAC of the code, never the code.</summary>
[BsonIgnoreExtraElements]
public sealed class CodeChallengeDocument
{
    /// <summary>The normalized address.</summary>
    [BsonId]
    public string Email { get; set; } = string.Empty;

    /// <summary>sign-in or register.</summary>
    public string Purpose { get; set; } = string.Empty;

    public byte[] Mac { get; set; } = [];

    public int AttemptsLeft { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastSentAt { get; set; }

    /// <summary>The TTL index deletes the challenge once this has passed.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }

    [BsonIgnoreIfNull]
    public RegistrationDetailsDocument? Registration { get; set; }
}

public sealed class CodeChallengeStore
{
    public const string SIGN_IN = "sign-in";
    public const string REGISTER = "register";

    readonly IMongoCollection<CodeChallengeDocument> _challenges;

    public CodeChallengeStore(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _challenges = context.Client.GetDatabase(settings.Database).GetCollection<CodeChallengeDocument>(settings.ChallengesCollection);
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
    public Task<CodeChallengeDocument?> SpendAttemptAsync(string email, DateTimeOffset now, CancellationToken cancellationToken = default)
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
