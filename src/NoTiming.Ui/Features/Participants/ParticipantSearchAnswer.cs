namespace NoTiming.Ui.Features.Participants;

/// <summary>What a search answered (#639, ADR-0010): the rows of one page, and whether another page follows.</summary>
public sealed class ParticipantSearchAnswer
{
    public ParticipantSearchAnswer(IReadOnlyList<FoundParticipant> rows, bool hasMore)
    {
        Rows = rows;
        HasMore = hasMore;
    }

    public IReadOnlyList<FoundParticipant> Rows { get; }
    public bool HasMore { get; }
}
