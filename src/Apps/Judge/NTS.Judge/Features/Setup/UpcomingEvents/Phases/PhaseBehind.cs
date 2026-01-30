using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseBehind : KrudServiceBase<Phase, PhaseFormModel>
{
    public PhaseBehind(IRepository<Phase> phases, IEnumerable<IKrudMirror<Phase>> reflections)
        : base(phases, reflections) { }

    protected override Phase CreateEntity(PhaseFormModel model)
    {
        return new Phase(model.Loop, model.Recovery, model.Rest, model.Id);
    }
}
