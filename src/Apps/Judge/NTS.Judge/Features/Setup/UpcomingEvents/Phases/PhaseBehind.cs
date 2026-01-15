using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseBehind : KrudServiceBase<Phase, PhaseFormModel>
{
    public PhaseBehind(IRepository<Phase> phases, IEnumerable<IKrudMirror<Phase>> reflections)
        : base(phases, reflections) { }

    protected override Phase CreateEntity(PhaseFormModel model)
    {
        return Phase.Create(model.Loop, model.Recovery, model.Rest);
    }

    protected override Phase UpdateEntity(PhaseFormModel model)
    {
        return Phase.Update(model.Id, model.Loop, model.Recovery, model.Rest);
    }
}
