namespace NTS.Domain.Setup.Services.Identities;

/// <summary>What is being matched: an Athlete's FEI ID is a number and a Horse's is text, and that is the one difference (ADR-0010).</summary>
public enum IdentityKind
{
    Athlete = 1,
    Horse = 2,
}
