using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Arrivelists;

namespace NTS.Application.Contracts.Arrivelists;

public interface IArrivelistService : IStatefulService
{
    IReadOnlyList<ArrivelistEntry> Entries { get; }
    void Tick();
}
