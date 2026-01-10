using Not.Blazor.Ports;
using Not.Injection;

namespace NTS.Judge.Features.Core;

public interface ICoreService : IStatefulService, ISingleton
{
    bool IsStarted { get; }
    Task Start();
    Task SoftReset();
    Task HardReset();
    Task LoadArchive(int archiveId);
}
