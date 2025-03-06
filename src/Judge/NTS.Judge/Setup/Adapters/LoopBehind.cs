using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Loops.Dot;

namespace NTS.Judge.Setup.Adapters;

public class LoopBehind : CrudBehind<Loop, LoopFormModel>
{
    public LoopBehind(IRepository<Loop> loopRepository)
        : base(loopRepository) { }

    protected override Loop CreateEntity(LoopFormModel model)
    {
        return Loop.Create(model.Distance);
    }

    protected override Loop UpdateEntity(LoopFormModel model)
    {
        return Loop.Update(model.Id, model.Distance);
    }
}
