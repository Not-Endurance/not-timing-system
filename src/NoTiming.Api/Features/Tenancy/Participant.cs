using System.Globalization;
using NTS.Domain.Enums;
using NTS.Domain.Helpers;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>What a Participant is (CONTEXT.md, ADR-0010): an Athlete or a Horse, the two kinds of row of the registry.</summary>
internal enum ParticipantKind
{
    Athlete = 1,
    Horse = 2,
}

/// <summary>
/// An Athlete or a Horse of a registry as the history of a Participant names it (#638): which it is, its id and the names it was
/// registered with, found across Tenants. Nothing else of the row leaves: not its FEI ID, its Club, its country or the account
/// it was once linked to.
/// </summary>
internal sealed class Participant
{
    public Participant(ParticipantKind kind, Guid id, string name, string? nameEnglish)
    {
        Kind = kind;
        Id = id;
        Name = name;
        NameEnglish = nameEnglish;
    }

    public ParticipantKind Kind { get; }
    public Guid Id { get; }
    public string Name { get; }
    public string? NameEnglish { get; }

    /// <summary>
    /// The Participant as a response tells it. The name is the one the person reads in their language, as the displays of the
    /// application render it (<see cref="NameRenderingHelper"/>): in English for a FEI competition and for a language that is
    /// English, the name as it was registered otherwise.
    /// </summary>
    /// <param name="ruleset">The ruleset of the competition the name is shown for; none outside of one.</param>
    public ParticipantName NameIn(CompetitionRuleset? ruleset, CultureInfo culture)
    {
        return new ParticipantName(Kind, Id, NameRenderingHelper.Render(Name, NameEnglish, ruleset, culture));
    }
}

/// <summary>
/// A Participant as the responses tell it, in the same three members wherever it is told (the other half of a ride, a match of
/// the search, a link of a person): <c>kind</c>, <c>id</c> and <c>displayName</c>.
/// </summary>
internal sealed class ParticipantName
{
    public ParticipantName(ParticipantKind kind, Guid id, string displayName)
    {
        Kind = kind;
        Id = id;
        DisplayName = displayName;
    }

    public ParticipantKind Kind { get; }
    public Guid Id { get; }
    public string DisplayName { get; }
}
