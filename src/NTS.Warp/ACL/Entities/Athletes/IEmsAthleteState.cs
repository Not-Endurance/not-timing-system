using NTS.Warp.ACL.Abstractions;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL.Entities.Athletes;

public interface IEmsAthleteState : IEmsIdentifiable
{
    public string FeiId { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string Club { get; }
    public EmsCategory Category { get; }
}
