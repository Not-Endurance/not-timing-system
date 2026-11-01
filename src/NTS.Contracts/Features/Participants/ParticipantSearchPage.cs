namespace NTS.Contracts.Features.Participants;

/// <summary>
/// A page of the Athletes or of the Horses that a search found, in the order of their names, and whether there is a next one
/// (#639, ADR-0010). A search is made of one kind at a time: the Athletes and the Horses are two registries.
/// </summary>
public sealed class ParticipantSearchPage
{
    public ParticipantSearchPage(IReadOnlyList<ParticipantName> items, bool hasMore)
    {
        Items = items;
        HasMore = hasMore;
    }

    public IReadOnlyList<ParticipantName> Items { get; }
    public bool HasMore { get; }
}
