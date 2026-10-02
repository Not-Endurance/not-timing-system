using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Presentlists;

namespace NTS.Contracts.Presentlists;

public interface IPresentlistService : IStatefulService
{
    IReadOnlyList<PresentlistEntry> Entries { get; }
    void Tick();
}
