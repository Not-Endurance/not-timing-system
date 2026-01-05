using Not.Collections;
using Not.Injection;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

// TODO: merge with StartlistService after domain event redesign, because circular dependency issue will be resolved then
public class StartlistContext : IStartlistContext
{
    public Startlist? Startlist { get; set; }

    public void Update(StartlistEntry entry, NCollectionAction action)
    {
        switch (action)
        {
            case NCollectionAction.Remove:
                Startlist?.Remove(entry.Number);
                break;
            case NCollectionAction.AddOrUpdate:
                Startlist?.Add(entry);
                break;
            default:
                break;
        }
    }
}

public interface IStartlistContext
{
    Startlist? Startlist { get; }
    void Update(StartlistEntry entry, NCollectionAction action);
}
