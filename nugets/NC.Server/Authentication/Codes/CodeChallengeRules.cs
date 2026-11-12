namespace NC.Server.Authentication.Codes;

/// <summary>
/// The rules of a one-time code: ten minutes, five checks and single use. A check spends an attempt before the code is
/// compared, so a correct fifth code is accepted and a correct sixth one is not.
/// </summary>
internal static class CodeChallengeRules
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
