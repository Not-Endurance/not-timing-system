using NTS.Contracts.Features.Participants;
using NTS.Domain.Helpers;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// An Athlete or a Horse as a search found it (#639, ADR-0010): with the values that the Api matched the words of the text
/// against, which is what a search for a text that only grows is answered from. What a person reads of it is made when it is
/// asked for, in the language the app is in then.
/// </summary>
public sealed class FoundParticipant
{
    public FoundParticipant(ParticipantKind kind, Guid id, string name, string? nameEnglish, string? feiId)
    {
        Kind = kind;
        Id = id;
        Name = name;
        NameEnglish = nameEnglish;
        FeiId = feiId;
    }

    public ParticipantKind Kind { get; }
    public Guid Id { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
    public string? FeiId { get; }

    /// <summary>
    /// Whether the word is part of the name, of the name in English or of the FEI ID, in any case: what the Api matches each
    /// word of a text against. The FEI ID is there only when the Api told it, which it does to the people who set an Event up,
    /// and then it matches it too: a row tells this code what the Api matched.
    /// </summary>
    public bool Has(string word)
    {
        return Contains(Name, word) || Contains(NameEnglish, word) || Contains(FeiId, word);
    }

    /// <summary>The Participant as a person reads it, in the language the app is in at this moment.</summary>
    public ParticipantName Named()
    {
        return new ParticipantName(Kind, Id, NameRenderingHelper.Render(Name, NameEnglish));
    }

    static bool Contains(string? text, string word)
    {
        return text != null && text.Contains(word, StringComparison.OrdinalIgnoreCase);
    }
}
