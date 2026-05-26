using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Presentlists;

namespace NTS.Application.Contracts.Presentlists;

public interface IPresentlistService : IStatefulService
{
    IReadOnlyList<PresentlistEntry> Entries { get; }
    bool CanAcknowledge { get; }
    Task Acknowledge(PresentlistEntry entry);
    void Tick();
}
