using Newtonsoft.Json;
using Not.Domain.Base;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Aggregates;

public class Official : AggregateRoot, IAggregateRoot
{
    public static Official Create(string? names, OfficialRole? role)
    {
        return new(Person.Create(names), role);
    }

    public static Official Update(int? id, string? names, OfficialRole? role)
    {
        return new(id, Person.Create(names), role);
    }

    [JsonConstructor]
    Official(int? id, Person? person, OfficialRole? role)
        : base(id!.Value)
    {
        Role = Required(nameof(Role), role);
        Person = Required(nameof(Person), person);
    }

    Official(Person? person, OfficialRole? role)
        : this(GenerateId(), person, role) { }

    public Person Person { get; }
    public OfficialRole Role { get; }

    public override string ToString()
    {
        var values = Role.GetDescription();
        return Combine(values, Person);
    }

    public bool IsUniqueRole()
    {
        return Role
            is VeterinaryCommissionPresident
                or GroundJuryPresident
                or ForeignVeterinaryDelegate
                or TechnicalDelegate
                or ForeignJudge;
    }
}
