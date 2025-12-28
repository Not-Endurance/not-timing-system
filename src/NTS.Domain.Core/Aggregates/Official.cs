using System.Xml.Linq;
using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates;

public class Official : AggregateRoot, IAggregateRoot
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Official(int id, Person? person, OfficialRole? role)
        : base(id)
    {
        Person = Required(nameof(Person), person);
        Role = Required(nameof(Role), role);
    }

    public Official(Person? person, OfficialRole? role)
        : this(GenerateId(), person, role) { }

    public Person Person { get; }
    public OfficialRole Role { get; }

    public override string ToString()
    {
        return $"{Role.GetDescription()}: {Person}";
    }
}
