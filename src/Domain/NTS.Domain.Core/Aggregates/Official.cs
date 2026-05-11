namespace NTS.Domain.Core.Aggregates;

public class Official : Aggregate, IEventScoped
{
    public Official(Person? person, OfficialRole? role, int eventId, int? id = null, int? userId = null)
        : base(id)
    {
        EventId = eventId;
        Person = Required(nameof(Person), person);
        Role = Required(nameof(Role), role);
        UserId = userId;
    }

    public int EventId { get; }
    public Person Person { get; }
    public OfficialRole Role { get; }
    public int? UserId { get; }

    public override string ToString()
    {
        return $"{Role.GetDescription()}: {Person}";
    }
}
