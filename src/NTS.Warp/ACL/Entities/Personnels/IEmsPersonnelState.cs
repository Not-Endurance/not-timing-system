using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Personnels;

public interface IEmsPersonnelState : IEmsIdentifiable
{
    public string Name { get; }

    public PersonnelRole Role { get; }
}
