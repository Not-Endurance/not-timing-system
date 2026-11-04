using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Objects.Arrivelists;

namespace NoTiming.Application.Contracts.Arrivelists;

public interface IArrivelistService : IStatefulService
{
    IReadOnlyList<ArrivelistEntry> Entries { get; }
    void Tick();
}
