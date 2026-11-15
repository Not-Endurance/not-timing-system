namespace NC.Server.Authentication.Passkeys;

public enum PasskeyOutcomeKind
{
    Done = 1,
    NotFound = 2,
    LastPasskey = 3,
    Refused = 4,
}
