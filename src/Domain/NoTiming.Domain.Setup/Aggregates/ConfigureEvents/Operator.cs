using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Aggregates.ConfigureEvents;

public class Operator : Entity
{
    public Operator(User? user, int? id = null, OfficialRole? role = null)
        : base(id)
    {
        User = Required(nameof(User), user);
        Role = OfficialRole.Steward;
    }

    public User User { get; }
    public OfficialRole Role { get; }

    public override string ToString()
    {
        return User.ToString();
    }
}
