using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Loops;

public class LoopService : KrudServiceBase<Loop, LoopFormModel>, ITransient
{
    public LoopService(IEnumerable<IKrudMirrorService<Loop>> dependants, IRepository<Loop> loops)
        : base(loops, dependants) { }

    protected override Loop MapEntity(LoopFormModel model)
    {
        return new(model.Distance, model.Id);
    }
}
