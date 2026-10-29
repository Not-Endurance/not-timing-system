namespace NoTiming.Api.Features.EventData;

/// <summary>
/// Where another family of what an Event keeps names the rows of a family by id: the collection that holds the rows that
/// name them, and the member, as a path in the stored document, that holds the id. A Ranking names the Participations it
/// counts in the entries it holds, and a Handout the Participation it is of (ADR-0006), so a Participation that one of
/// them still names is not removed: what they show could not be composed without it.
/// </summary>
internal sealed class EventDataReference
{
    public EventDataReference(string collection, string member)
    {
        Collection = collection;
        Member = member;
    }

    public string Collection { get; }
    public string Member { get; }
}
