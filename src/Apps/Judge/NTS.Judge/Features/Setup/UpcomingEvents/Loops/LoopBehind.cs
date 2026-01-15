using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopBehind : CrudChildBehind<Loop, LoopFormModel>
{
    public LoopBehind(IEnumerable<ICrudReflection<Loop>> dependants, UpcomingEventCrudeContext parentContext)
        : base(dependants, parentContext) { }

    protected override Loop CreateEntity(LoopFormModel model)
    {
        return Loop.Create(model.Distance);
    }

    protected override Loop UpdateEntity(LoopFormModel model)
    {
        return Loop.Update(model.Id, model.Distance);
    }
}
