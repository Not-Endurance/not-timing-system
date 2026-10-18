using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// One Athlete or one Horse as some document stores it (#636, ADR-0010): what it says of itself, and where it was found. A
/// Setup row is the registry as it is now; every other copy is embedded in the document that holds it.
/// </summary>
public sealed class IdentityCopy
{
    public required IdentityKind Kind { get; init; }

    /// <summary>The collection of the document that holds the copy.</summary>
    public required string Collection { get; init; }

    /// <summary>The id of that document, as text.</summary>
    public required string DocumentId { get; init; }

    /// <summary>Where in the document the copy is: <c>Competitions[0].Participations[2].Combination.Athlete</c>, none for a Setup row.</summary>
    public required string Path { get; init; }

    /// <summary>A copy that is a document of <c>athletes</c> or <c>horses</c> itself: a row of the registry.</summary>
    public required bool IsSetupRow { get; init; }

    /// <summary>The integer id the copy had before ids were Guids, as text; none for 0, for no id and for an id that is not an integer.</summary>
    public string? LegacyId { get; init; }

    public string? Name { get; init; }
    public string? NameEnglish { get; init; }
    public string? FeiId { get; init; }
    public string? Country { get; init; }
    public string? Club { get; init; }

    /// <summary>The Event the document belongs to: the configure event itself, or the Event a Participation, a Ranking or a Handout names.</summary>
    public string? EventId { get; init; }

    /// <summary>The Participation the copy is a part of: one of a Setup or of a started Event, or the copy of one in a Ranking or a Handout.</summary>
    public string? ParticipationId { get; init; }

    /// <summary>The name of the other half of its Combination: the Horse of an Athlete, the Athlete of a Horse.</summary>
    public string? Partner { get; init; }
}
