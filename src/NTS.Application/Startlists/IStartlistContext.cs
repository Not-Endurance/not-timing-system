using Not.Collections;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public interface IStartlistContext
{
    Startlist? Startlist { get; set; }
    void Update(Participation participation, NCollectionAction action);
}
