using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseBehind : CrudChildBehind<Phase, PhaseFormModel>
{
    public PhaseBehind(CompetitionCrudeContext crudeContext, IEnumerable<ICrudReflection<Phase>> reflections)
        : base(reflections, crudeContext) { }

    protected override Phase CreateEntity(PhaseFormModel model)
    {
        return Phase.Create(model.Loop, model.Recovery, model.Rest);
    }

    protected override Phase UpdateEntity(PhaseFormModel model)
    {
        return Phase.Update(model.Id, model.Loop, model.Recovery, model.Rest);
    }
    //
    // public async Task Reflect(Loop loop)
    // {
    //     await UpdateReflections(x => x.Loop, loop, phase => phase.Reflect(loop));
    // }
}
