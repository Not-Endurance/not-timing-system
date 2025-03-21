using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Blazor.Shared.Components.SidePanels;
using NTS.Judge.Core.Start;

namespace NTS.Judge.Core.Behinds.Adapters;

public class CoreBehind : ObservableBehind, ICoreBehind
{
    readonly ICoreStarter _coreStarter;
    readonly IRepository<EnduranceEvent> _enduranceEvents;

    public CoreBehind(ICoreStarter coreStarter, IRepository<EnduranceEvent> enduranceEvents)
    {
        _coreStarter = coreStarter;
        _enduranceEvents = enduranceEvents;
    }

    public bool IsStarted { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var enduranceEvents = await _enduranceEvents.Read(0);
        IsStarted = enduranceEvents != null;
        return IsStarted;
    }

    public Task Start()
    {
        return SafeHelper.Run(SafeStart);
    }

    async Task SafeStart()
    {
        await _coreStarter.Start();
        IsStarted = true;
        EmitChange();
    }
}
