namespace NTS.Domain.Core.Aggregates;

public class Handout : Aggregate, IEventScoped
{
    public Handout(Participation participation, int? id = null)
        : base(id)
    {
        Participation = participation;
        EventId = participation.EventId;
    }

    public int EventId { get; }
    public Participation Participation { get; }
}
