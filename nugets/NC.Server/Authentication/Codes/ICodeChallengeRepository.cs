namespace NC.Server.Authentication.Codes;

/// <summary>The one open code challenge per address. It holds the MAC of the code, never the code.</summary>
internal interface ICodeChallengeRepository
{
    Task<CodeChallengeDocument?> Find(string email, CancellationToken cancellationToken = default);

    /// <summary>A new request replaces the previous code, so only the latest code works.</summary>
    Task Replace(CodeChallengeDocument challenge, CancellationToken cancellationToken = default);

    /// <summary>Uses up one attempt, and returns the challenge with the attempt spent. Null when none can be spent.</summary>
    Task<CodeChallengeDocument?> SpendAttempt(
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes the challenge only while it still holds this MAC, so a code is used once even under a race.</summary>
    Task<bool> Consume(string email, byte[] mac, CancellationToken cancellationToken = default);
}
