namespace NC.Server.Authentication.SignIn;

/// <summary>The answer to a request for a code. It is the same whether or not the address has an account, unless the limits refuse it.</summary>
public sealed class CodeRequestOutcome
{
    public bool Accepted { get; init; }

    public TimeSpan RetryAfter { get; init; }
}
