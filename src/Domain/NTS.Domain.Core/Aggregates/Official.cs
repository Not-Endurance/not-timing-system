namespace NTS.Domain.Core.Aggregates;

public class Official : Aggregate
{
    public Official(int? id, Person? person, OfficialRole? role)
        : base(id)
    {
        Person = Required(nameof(Person), person);
        Role = Required(nameof(Role), role);
    }

    public Person Person { get; }
    public OfficialRole Role { get; }

    public override string ToString()
    {
        return $"{Role.GetDescription()}: {Person}";
    }
}
