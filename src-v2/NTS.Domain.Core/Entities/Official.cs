namespace NTS.Domain.Core.Entities;

public class Official : DomainEntity
{
    private Official(int id, Person? person, OfficialRole? role) : base(id)
    {
        Person = Required(nameof(Person), person);
        Role = Required(nameof(Role), role);
    }
    public Official(Person? person, OfficialRole? role) : this(GenerateId(), person, role)
    {
    }

    public Person Person { get; private set; }
    public OfficialRole Role { get; private set; }

    public override string ToString()
    {
        return $"{Role}: {Person}";
    }
}
