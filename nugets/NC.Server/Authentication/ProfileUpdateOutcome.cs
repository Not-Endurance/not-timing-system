namespace NC.Server.Authentication;

/// <summary>The answer to a profile change.</summary>
public enum ProfileUpdateOutcome
{
    Saved,
    MissingDetails,
    NotSignedIn,
}
