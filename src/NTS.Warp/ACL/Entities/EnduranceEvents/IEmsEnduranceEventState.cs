using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.EnduranceEvents;

public interface IEmsEnduranceEventState : IEmsIdentifiable
{
    public string Name { get; }
    public string PopulatedPlace { get; }
    public bool HasStarted { get; }

    public string FeiCode { get; }
    public string ShowFeiId { get; }
    public string FeiId { get; }
}
