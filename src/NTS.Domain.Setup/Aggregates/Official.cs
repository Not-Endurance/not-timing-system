using Newtonsoft.Json;
using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Aggregates;

public class Official : AggregateRoot
{
    public static Official Create(string? names, OfficialRole? role)
    {
        return new(Person.Create(names), role);
    }

    public static Official Update(int? id, string? names, OfficialRole? role)
    {
        return new(id, Person.Create(names), role);
    }

    Official(Person? person, OfficialRole? role)
        : this(GenerateId(), person, role) { }

    [JsonConstructor]
    public Official(int? id, Person? person, OfficialRole? role)
        : base(id!.Value)
    {
        Role = Required(nameof(Role), role);
        Person = Required(nameof(Person), person);
    }

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
