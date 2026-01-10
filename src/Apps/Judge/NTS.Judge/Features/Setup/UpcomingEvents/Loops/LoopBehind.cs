using Not.Application.Krud;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopBehind : KrudService<Loop, LoopFormModel>
{
    public LoopBehind(IEnumerable<IKrudMirror<Loop>> dependants, UpcomingEventKrudRoot parentContext)
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
