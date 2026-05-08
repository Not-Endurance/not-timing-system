using NTS.Domain.Setup.Aggregates;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Aggregates.ConfigureEvents;

public class Official : Entity
{
    public Official(Person? person, OfficialRole? role, int? id = null, User? user = null)
        : base(id)
    {
        Role = Required(nameof(Role), role);
        Person = Required(nameof(Person), person);
        User = user;
    }

    public Person Person { get; }
    public OfficialRole Role { get; }
    public User? User { get; }

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
