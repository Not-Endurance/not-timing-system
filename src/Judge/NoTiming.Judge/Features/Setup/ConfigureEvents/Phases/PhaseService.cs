using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Features.Setup.ConfigureEvents.Phases;

public class PhaseService : KrudServiceBase<Phase, PhaseFormModel>, ITransient
{
    public PhaseService(IRepository<Phase> phases, IEnumerable<IKrudMirrorService<Phase>> reflections)
        : base(phases, reflections) { }
}
