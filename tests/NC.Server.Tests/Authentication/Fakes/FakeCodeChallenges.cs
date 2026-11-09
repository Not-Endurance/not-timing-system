using NC.Server.Authentication.Codes;

namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>One open challenge per address, spent and consumed the way the store does it.</summary>
internal sealed class FakeCodeChallenges : ICodeChallengeRepository
{
    CodeChallengeDocument? _challenge;

    public void Store(CodeChallengeDocument challenge)
    {
        _challenge = challenge;
    }

    public Task<CodeChallengeDocument?> Find(string email, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_challenge is null ? null : Copy(_challenge));
    }

    public Task Replace(CodeChallengeDocument challenge, CancellationToken cancellationToken = default)
    {
        _challenge = challenge;
        return Task.CompletedTask;
    }

    public Task<CodeChallengeDocument?> SpendAttempt(
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        if (_challenge is null || _challenge.AttemptsLeft <= 0)
        {
            return Task.FromResult<CodeChallengeDocument?>(null);
        }

        _challenge.AttemptsLeft--;
        return Task.FromResult<CodeChallengeDocument?>(Copy(_challenge));
    }

    public Task<bool> Consume(string email, byte[] mac, CancellationToken cancellationToken = default)
    {
        if (_challenge is null || !_challenge.Mac.AsSpan().SequenceEqual(mac))
        {
            return Task.FromResult(false);
        }

        _challenge = null;
        return Task.FromResult(true);
    }

    static CodeChallengeDocument Copy(CodeChallengeDocument challenge)
    {
        return new CodeChallengeDocument
        {
            Email = challenge.Email,
            Purpose = challenge.Purpose,
            Mac = challenge.Mac,
            AttemptsLeft = challenge.AttemptsLeft,
            LastSentAtUtc = challenge.LastSentAtUtc,
            ExpiresAtUtc = challenge.ExpiresAtUtc,
            Registration = challenge.Registration,
        };
    }
}
