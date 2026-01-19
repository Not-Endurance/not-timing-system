using Newtonsoft.Json;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Aggregates.UpcomingEvents;

public class Official : Entity
{
    [JsonConstructor]
    public Official(Person? person, OfficialRole? role)
        : base(person)
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
