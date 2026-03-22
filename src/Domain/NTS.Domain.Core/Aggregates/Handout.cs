namespace NTS.Domain.Core.Aggregates;

public class Handout : Aggregate
{
    public Handout(Participation participation)
        : base(participation.Id)
    {
        Participation = participation;
        EventId = participation.EventId;
    }

    public int EventId { get; }
    public Participation Participation { get; }
}
