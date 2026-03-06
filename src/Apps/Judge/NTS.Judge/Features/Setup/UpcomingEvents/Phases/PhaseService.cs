using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseService : KrudServiceBase<Phase, PhaseFormModel>, ITransient
{
    public PhaseService(IRepository<Phase> phases, IEnumerable<IKrudMirror<Phase>> reflections)
        : base(phases, reflections) { }
}
