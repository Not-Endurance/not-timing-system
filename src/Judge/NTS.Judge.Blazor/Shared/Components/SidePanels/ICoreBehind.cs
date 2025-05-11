using Not.Blazor.Ports;
using Not.Injection;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels;

public interface ICoreBehind : IObservableBehind
{
    bool IsStarted { get; }
    Task Start();
    Task Reset();
    Task LoadArchive(int archiveId);
}
