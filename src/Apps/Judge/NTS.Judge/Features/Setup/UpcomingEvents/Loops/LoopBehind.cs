using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopBehind : KrudServiceBase<Loop, LoopFormModel>
{
    public LoopBehind(IEnumerable<IKrudMirror<Loop>> dependants, IRepository<Loop> loops)
        : base(loops, dependants) { }

    protected override Loop CreateEntity(LoopFormModel model)
    {
        return Loop.Create(model.Distance);
    }

    protected override Loop UpdateEntity(LoopFormModel model)
    {
        return Loop.Update(model.Id, model.Distance);
    }
}
