using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopBehind : KrudServiceBase<Loop, LoopFormModel>
{
    public LoopBehind(IEnumerable<IKrudMirror<Loop>> dependants, IRepository<Loop> loops)
        : base(loops, dependants) { }

    protected override Loop CreateEntity(LoopFormModel model)
    {
        return new(model.Distance, model.Id);
    }
}
