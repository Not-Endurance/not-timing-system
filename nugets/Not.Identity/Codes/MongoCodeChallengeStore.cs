using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Not.Identity.Codes;

public sealed class MongoCodeChallengeStore : ICodeChallengeStore
{
    const string PROTECTOR_PURPOSE = "Not.Identity.Codes.v1";
    const int CODE_RANGE = 1_000_000;

    readonly IMongoCollection<CodeChallenge> _challenges;
    readonly IDataProtector _protector;
    readonly NIdentityOptions _options;
    readonly TimeProvider _time;

    public MongoCodeChallengeStore(
        IMongoClient client,
        IOptions<NIdentityOptions> options,
        IDataProtectionProvider dataProtection,
        TimeProvider time
    )
    {
        _options = options.Value;
        _challenges = client.GetDatabase(_options.Database).GetCollection<CodeChallenge>(_options.ChallengesCollection);
        _protector = dataProtection.CreateProtector(PROTECTOR_PURPOSE);
        _time = time;
    }

    public async Task<CodeIssue> IssueAsync(
        string email,
        string purpose,
        BsonDocument? pending = null,
        CancellationToken cancellationToken = default
    )
    {
        var now = _time.GetUtcNow();
        var code = GenerateCode();
        var protectedCode = _protector.Protect(code);
        var createdAt = now.UtcDateTime;
        var expiresAt = (now + _options.CodeLifetime).UtcDateTime;
        var cooledDownBefore = (now - _options.CodeResendCooldown).UtcDateTime;

        // A challenge that is past its cooldown is replaced in place, so there is only ever one per address and purpose.
        var replaced = await _challenges.UpdateOneAsync(
            Builders<CodeChallenge>.Filter.And(
                Builders<CodeChallenge>.Filter.Eq(x => x.Email, email),
                Builders<CodeChallenge>.Filter.Eq(x => x.Purpose, purpose),
                Builders<CodeChallenge>.Filter.Lte(x => x.CreatedAt, cooledDownBefore)
            ),
            Builders<CodeChallenge>
                .Update.Set(x => x.ProtectedCode, protectedCode)
                .Set(x => x.Attempts, 0)
                .Set(x => x.CreatedAt, createdAt)
                .Set(x => x.ExpiresAt, expiresAt)
                .Set(x => x.Pending, pending),
            cancellationToken: cancellationToken
        );
        if (replaced.MatchedCount == 1)
        {
            return new CodeIssue(CodeIssueStatus.Issued, code);
        }

        try
        {
            await _challenges.InsertOneAsync(
                new CodeChallenge
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Purpose = purpose,
                    ProtectedCode = protectedCode,
                    CreatedAt = createdAt,
                    ExpiresAt = expiresAt,
                    Pending = pending,
                },
                cancellationToken: cancellationToken
            );
            return new CodeIssue(CodeIssueStatus.Issued, code);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // The unique index on address and purpose: one is already inside its cooldown.
            return new CodeIssue(CodeIssueStatus.CoolingDown, null);
        }
    }

    public async Task<CodeVerification> VerifyAsync(
        string email,
        string purpose,
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var now = _time.GetUtcNow().UtcDateTime;

        // The attempt is counted before the code is looked at, atomically, so concurrent guesses cannot exceed the limit.
        var challenge = await _challenges.FindOneAndUpdateAsync(
            Builders<CodeChallenge>.Filter.And(
                Builders<CodeChallenge>.Filter.Eq(x => x.Email, email),
                Builders<CodeChallenge>.Filter.Eq(x => x.Purpose, purpose),
                Builders<CodeChallenge>.Filter.Gt(x => x.ExpiresAt, now),
                Builders<CodeChallenge>.Filter.Lt(x => x.Attempts, _options.CodeMaxAttempts)
            ),
            Builders<CodeChallenge>.Update.Inc(x => x.Attempts, 1),
            new FindOneAndUpdateOptions<CodeChallenge> { ReturnDocument = ReturnDocument.After },
            cancellationToken
        );
        if (challenge is null)
        {
            return CodeVerification.Failed;
        }

        if (Matches(challenge.ProtectedCode, code))
        {
            // Single use: of concurrent attempts with the right code only the one that deletes the challenge succeeds.
            var consumed = await _challenges.DeleteOneAsync(x => x.Id == challenge.Id, cancellationToken);
            return consumed.DeletedCount == 1 ? new CodeVerification(true, challenge.Pending) : CodeVerification.Failed;
        }

        if (challenge.Attempts >= _options.CodeMaxAttempts)
        {
            await _challenges.DeleteOneAsync(x => x.Id == challenge.Id, cancellationToken);
        }

        return CodeVerification.Failed;
    }

    static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(CODE_RANGE).ToString("D6", CultureInfo.InvariantCulture);
    }

    bool Matches(string protectedCode, string code)
    {
        var offered = new string(code.Where(x => !char.IsWhiteSpace(x)).ToArray());
        try
        {
            var expected = _protector.Unprotect(protectedCode);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(offered)
            );
        }
        catch (CryptographicException)
        {
            // The key ring that protected it is gone: nothing can match, and the code expires anyway.
            return false;
        }
    }
}
