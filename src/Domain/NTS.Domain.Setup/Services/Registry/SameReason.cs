namespace NTS.Domain.Setup.Services.Registry;

/// <summary>Why a row of the registry is taken for the same Athlete or Horse as the one that is being registered.</summary>
public enum SameReason
{
    /// <summary>It has the same name after trimming, collapsing spaces and case-folding, in either script.</summary>
    Name = 0,

    /// <summary>It has the same FEI ID: an Athlete's compared as a number, a Horse's trimmed and case-folded.</summary>
    FeiId = 1,
}
