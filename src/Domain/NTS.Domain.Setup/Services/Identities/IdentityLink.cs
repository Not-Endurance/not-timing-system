namespace NTS.Domain.Setup.Services.Identities;

/// <summary>Two copies and the rule that decided how they relate (#635).</summary>
public sealed class IdentityLink
{
    public IdentityLink(IdentityObservation one, IdentityObservation another, MatchClass match)
    {
        One = one;
        Another = another;
        Class = match;
    }

    public IdentityObservation One { get; }
    public IdentityObservation Another { get; }
    public MatchClass Class { get; }
}
