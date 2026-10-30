namespace NTS.Domain.Core.Aggregates;

/// <summary>
/// The sheet of one Participation that is handed out after a completed Phase (ADR-0006). It names the Participation and
/// holds none of its data, so what it shows is the Participation as it is when the document is composed, corrections
/// included.
/// </summary>
public sealed class Handout : Aggregate, IEventScoped
{
    public Handout(Guid eventId, Guid participationId, Guid? id = null)
        : base(id)
    {
        EventId = eventId;
        ParticipationId = NotDefault(nameof(ParticipationId), participationId);
    }

    public Guid EventId { get; }
    public Guid ParticipationId { get; }

    public override string ToString()
    {
        return $"Handout of {ParticipationId}";
    }
}
