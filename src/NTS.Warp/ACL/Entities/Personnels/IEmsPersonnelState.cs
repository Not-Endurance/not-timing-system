using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Personnels;

public interface IEmsPersonnelState : IEmsIdentifiable
{
    public string Name { get; }

    public PersonnelRole Role { get; }
}
