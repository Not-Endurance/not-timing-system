namespace NTS.Domain.Core.Aggregates;

public class Official : Aggregate
{
    public Official(Person? person, OfficialRole? role, int eventId, int id)
        : base(id)
    {
        EventId = eventId;
        Person = Required(nameof(Person), person);
        Role = Required(nameof(Role), role);
    }

    public int EventId { get; }
    public Person Person { get; }
    public OfficialRole Role { get; }

    public override string ToString()
    {
        return $"{Role.GetDescription()}: {Person}";
    }
}
