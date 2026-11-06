namespace NoTiming.Api.Application.Authentication.Codes;

/// <summary>What a code challenge still allows: when it ends, how many checks are left, and whether it was used.</summary>
public sealed record CodeChallengeState
{
    public DateTimeOffset ExpiresAtUtc { get; init; }

    public int AttemptsLeft { get; init; }

    public bool Used { get; init; }
}

public sealed record CodeVerification
{
    public bool Accepted { get; init; }

    public CodeChallengeState State { get; init; } = new();
}

/// <summary>
/// The rules of a one-time code: ten minutes, five checks and single use. A check spends an attempt before the code is
/// compared, so a correct fifth code is accepted and a correct sixth one is not.
/// </summary>
public static class CodeChallengeRules
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly int MaxAttempts = 5;

    public static CodeChallengeState Issue(DateTimeOffset now)
    {
        return new CodeChallengeState
        {
            ExpiresAtUtc = now.Add(Lifetime),
            AttemptsLeft = MaxAttempts,
            Used = false,
        };
    }

    public static CodeVerification Verify(CodeChallengeState state, bool codeMatches, DateTimeOffset at)
    {
        if (state.Used || at >= state.ExpiresAtUtc || state.AttemptsLeft <= 0)
        {
            return new CodeVerification { Accepted = false, State = state };
        }

        var spent = state with { AttemptsLeft = state.AttemptsLeft - 1 };
        if (!codeMatches)
        {
            return new CodeVerification { Accepted = false, State = spent };
        }

        return new CodeVerification { Accepted = true, State = spent with { Used = true } };
    }
}
