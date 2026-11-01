namespace NTS.Domain.Setup.Services.Identities;

public static class MatchClassExtensions
{
    /// <summary>Whether the class says the copies are one without a person looking at them.</summary>
    public static bool IsAutomatic(this MatchClass match)
    {
        return match
            is MatchClass.SameFeiId
                or MatchClass.ExactName
                or MatchClass.StrictTransliteration
                or MatchClass.SameLegacyId;
    }
}
