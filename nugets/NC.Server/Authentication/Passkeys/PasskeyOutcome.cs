namespace NC.Server.Authentication.Passkeys;

public sealed class PasskeyOutcome
{
    public PasskeyOutcomeKind Kind { get; init; }

    public string? Name { get; init; }
}
