using Not.Collections;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

// TODO: merge with StartlistService after domain event redesign, because circular dependency issue will be resolved then
public class StartlistContext : IStartlistContext
{
    public Startlist? Startlist { get; set; }

    public void Update(Participation participation, NCollectionAction action)
    {
        switch (action)
        {
            case NCollectionAction.Remove:
                Startlist?.Remove(participation.Combination.Number);
                break;
            case NCollectionAction.AddOrUpdate:
                Startlist?.Add(participation);
                break;
            default:
                break;
        }
    }
}

public interface IStartlistContext
{
    Startlist? Startlist { get; set; }
    void Update(Participation participation, NCollectionAction action);
}
