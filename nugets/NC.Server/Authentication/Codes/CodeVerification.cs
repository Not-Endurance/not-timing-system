namespace NC.Server.Authentication.Codes;

public sealed record CodeVerification
{
    public bool Accepted { get; init; }

    public CodeChallengeState State { get; init; } = new();
}
