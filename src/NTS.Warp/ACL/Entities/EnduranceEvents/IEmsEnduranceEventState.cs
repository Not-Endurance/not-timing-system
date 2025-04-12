using NTS.Relay.ACL.Abstractions;

namespace Core.Domain.State.EnduranceEvents;

public interface IEmsEnduranceEventState : IEmsIdentifiable
{
    public string Name { get; }
    public string PopulatedPlace { get; }
    public bool HasStarted { get; }

    public string FeiCode { get; }
    public string ShowFeiId { get; }
    public string FeiId { get; }
}
