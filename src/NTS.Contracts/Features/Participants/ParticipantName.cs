namespace NTS.Contracts.Features.Participants;

/// <summary>
/// A Participant as the Api tells it, in the same three members wherever it is told (#638, #639): the other half of a ride, a
/// match of the search, a link of a person. The name is the one the person reads in their language, as the displays of the
/// application render it: in English for a FEI competition and for a language that is English, as it was registered otherwise.
/// </summary>
public sealed class ParticipantName
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
