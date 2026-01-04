using Not.Blazor.Ports;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels;

public interface ICoreService : INObservable
{
    bool IsStarted { get; }
    Task Start();
    Task SoftReset();
    Task HardReset();
    Task LoadArchive(int archiveId);
}
