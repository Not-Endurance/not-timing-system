using MongoDB.Bson;

namespace Not.Identity.Codes;

/// <summary>
/// One-time codes sent by email (ADR-0002): stored, single-use challenges, one per address and purpose. A code
/// expires, is invalidated by too many wrong attempts and works once. Attempts are counted per challenge and never
/// per account, so nobody can lock a real user out by guessing codes for their address.
/// </summary>
public interface ICodeChallengeStore
{
    /// <summary>
    /// Replaces the challenge of the address and purpose with a new code. Within the resend cooldown of the previous
    /// request it does nothing and says so, and the previous code stays valid.
    /// </summary>
    /// <param name="email">The normalized address.</param>
    /// <param name="purpose">What the code is for, see <see cref="CodePurposes"/>.</param>
    /// <param name="pending">Data that waits for the code to be verified, so nothing is stored for a person who never is.</param>
    Task<CodeIssue> IssueAsync(
        string email,
        string purpose,
        BsonDocument? pending = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Counts the attempt, and on the right code consumes the challenge. Only one of several concurrent attempts with
    /// the right code succeeds. Whatever is wrong (no challenge, expired, spent attempts, wrong code) answers alike.
    /// </summary>
    Task<CodeVerification> VerifyAsync(
        string email,
        string purpose,
        string code,
        CancellationToken cancellationToken = default
    );
}

public static class CodePurposes
{
    public const string SIGN_IN = "sign-in";
}

public enum CodeIssueStatus
{
    Issued,
    CoolingDown,
}

public sealed record CodeIssue
{
    /// <param name="status">Whether a new code was issued.</param>
    /// <param name="code">The code to send, only when issued. It is stored in protected form and cannot be read back.</param>
    public CodeIssue(CodeIssueStatus status, string? code)
    {
        Status = status;
        Code = code;
    }

    public CodeIssueStatus Status { get; }
    public string? Code { get; }
    public bool Issued => Status == CodeIssueStatus.Issued;
}

public sealed record CodeVerification
{
    /// <param name="succeeded">Whether the code was right, in time, and not spent.</param>
    /// <param name="pending">The data that waited for the code, when it succeeded.</param>
    public CodeVerification(bool succeeded, BsonDocument? pending)
    {
        Succeeded = succeeded;
        Pending = pending;
    }

    public static CodeVerification Failed { get; } = new(false, null);

    public bool Succeeded { get; }
    public BsonDocument? Pending { get; }
}
