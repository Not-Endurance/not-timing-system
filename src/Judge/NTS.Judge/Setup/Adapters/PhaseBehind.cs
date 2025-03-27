using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Phases;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class PhaseBehind : CrudChildBehind<Phase, PhaseFormModel>, ICrudReflection<Loop>
{
    public PhaseBehind(
        IRepository<Phase> phase,
        CompetitionParentContext parentContext,
        IEnumerable<ICrudReflection<Phase>> reflections
    )
        : base(phase, reflections, parentContext)
    {
    }

    protected override Phase CreateEntity(PhaseFormModel model)
    {
        return Phase.Create(model.Loop, model.Recovery, model.Rest);
    }

    protected override Phase UpdateEntity(PhaseFormModel model)
    {
        return Phase.Update(model.Id, model.Loop, model.Recovery, model.Rest);
    }

    public Task Reflect(Loop loop)
    {
        UpdateReflections(x => x.Loop, loop, phase => phase.Reflect(loop));
        return Task.CompletedTask;
    }
}
