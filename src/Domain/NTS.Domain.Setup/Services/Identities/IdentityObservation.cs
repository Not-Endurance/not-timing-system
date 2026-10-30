namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// One copy of an Athlete or a Horse as it was seen in some document (#635, ADR-0010): what it says of itself and nothing of where
/// it was found, apart from a key of the caller's own, by which the caller finds the document again. The country and the club
/// are evidence for whoever reviews a match and never decide one.
/// </summary>
public sealed class IdentityObservation
{
    public IdentityObservation(
        string key,
        string? legacyId,
        string? name,
        string? nameEnglish,
        string? feiId,
        string? country,
        string? club
    )
    {
        Key = key;
        LegacyId = legacyId;
        Name = name;
        NameEnglish = nameEnglish;
        FeiId = feiId;
        Country = country;
        Club = club;
    }

    /// <summary>What the caller calls this copy by: nothing here reads it.</summary>
    public string Key { get; }

    /// <summary>The id the copy had before ids were Guids, as the text of the number: 0, blank and none are no id.</summary>
    public string? LegacyId { get; }

    public string? Name { get; }
    public string? NameEnglish { get; }
    public string? FeiId { get; }
    public string? Country { get; }
    public string? Club { get; }
}
