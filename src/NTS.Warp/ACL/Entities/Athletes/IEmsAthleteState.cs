using NTS.Relay.ACL.Abstractions;
using NTS.Relay.ACL.Enums;

namespace Core.Domain.State.Athletes;

public interface IEmsAthleteState : IEmsIdentifiable
{
    public string FeiId { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string Club { get; }
    public EmsCategory Category { get; }
}
