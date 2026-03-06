using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopService : KrudServiceBase<Loop, LoopFormModel>, ITransient
{
    public LoopService(IEnumerable<IKrudMirror<Loop>> dependants, IRepository<Loop> loops)
        : base(loops, dependants) { }

    protected override Loop MapEntity(LoopFormModel model)
    {
        return new(model.Distance, model.Id);
    }
}
