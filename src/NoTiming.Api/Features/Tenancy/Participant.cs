using System.Globalization;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Enums;
using NTS.Domain.Helpers;

namespace NoTiming.Api.Features.Tenancy;

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
