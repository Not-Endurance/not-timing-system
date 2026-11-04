using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Phases;

public class PhaseService : KrudServiceBase<Phase, PhaseFormModel>, ITransient
{
    public PhaseService(IRepository<Phase> phases, IEnumerable<IKrudMirrorService<Phase>> reflections)
        : base(phases, reflections) { }
}
