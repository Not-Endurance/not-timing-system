using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Objects.Presentlists;

namespace NoTiming.Application.Contracts.Presentlists;

public interface IPresentlistService : IStatefulService
{
    IReadOnlyList<PresentlistEntry> Entries { get; }
    bool CanAcknowledge { get; }
    Task Acknowledge(PresentlistEntry entry);
    void Tick();
}
