namespace NTS.Domain.Core.Aggregates;

public class Operator : Aggregate, IEventScoped
{
    public Operator(Guid eventId, Guid? userId, OfficialRole? role = null, Guid? id = null)
        : base(id)
    {
        EventId = eventId;
        UserId = Required(nameof(UserId), userId);
        Role = OfficialRole.Steward;
    }

    public Guid EventId { get; }
    public Guid UserId { get; }
    public OfficialRole Role { get; }
}
