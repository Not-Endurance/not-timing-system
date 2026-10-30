using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>The Loops of the Setup that is open (#646).</summary>
public class SetupLoopService : KrudServiceBase<Loop, LoopFormModel>, ITransient
{
    public SetupLoopService(IEnumerable<IKrudMirrorService<Loop>> dependants, IRepository<Loop> loops)
        : base(loops, dependants) { }
}
