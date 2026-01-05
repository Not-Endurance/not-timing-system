using Not.Blazor.Ports;
using Not.Injection;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels;

public interface ICoreService : INObservable, ISingleton
{
    bool IsStarted { get; }
    Task Start();
    Task SoftReset();
    Task HardReset();
    Task LoadArchive(int archiveId);
}
