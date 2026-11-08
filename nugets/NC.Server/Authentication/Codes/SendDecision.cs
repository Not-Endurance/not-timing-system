namespace NC.Server.Authentication.Codes;

public sealed record SendDecision
{
    public bool Allowed { get; init; }

    public SendLimitScope? Scope { get; init; }

    public TimeSpan RetryAfter { get; init; }
}
