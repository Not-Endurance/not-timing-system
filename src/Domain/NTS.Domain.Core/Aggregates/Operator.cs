namespace NTS.Domain.Core.Aggregates;

public class Operator : Aggregate, IEventScoped
{
    public Operator(int eventId, int? userId, OfficialRole? role = null, int? id = null)
        : base(id)
    {
        EventId = eventId;
        UserId = Required(nameof(UserId), userId);
        Role = OfficialRole.Steward;
    }

    public int EventId { get; }
    public int UserId { get; }
    public OfficialRole Role { get; }
}
