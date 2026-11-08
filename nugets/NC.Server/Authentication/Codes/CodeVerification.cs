namespace NC.Server.Authentication.Codes;

internal sealed record CodeVerification
{
    public bool Accepted { get; init; }

    public CodeChallengeState State { get; init; } = new();
}
