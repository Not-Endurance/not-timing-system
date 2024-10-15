using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Pages.Setup.Loops;

namespace NTS.Judge.Events;

public class LoopBehind : CrudBehind<Loop, LoopFormModel>
{
    public LoopBehind(IRepository<Loop> loopRepository) : base(loopRepository)
    {
    }

    protected override Loop CreateEntity(LoopFormModel model)
    {
        return Loop.Create(model.Distance);
    }

    protected override Loop UpdateEntity(LoopFormModel model)
    {
        return Loop.Update(model.Id, model.Distance);
    }
}
