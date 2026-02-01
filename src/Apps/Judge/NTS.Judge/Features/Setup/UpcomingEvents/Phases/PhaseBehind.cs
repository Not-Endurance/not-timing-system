using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseBehind : KrudServiceBase<Phase, PhaseFormModel>
{
    public PhaseBehind(IRepository<Phase> phases, IEnumerable<IKrudMirror<Phase>> reflections)
        : base(phases, reflections) { }
}
