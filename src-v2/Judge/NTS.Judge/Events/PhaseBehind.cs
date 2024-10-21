using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Pages.Setup.Phases;
using NTS.Judge.Contexts;

namespace NTS.Judge.Events;

public class PhaseBehind : CrudBehind<Phase, PhaseFormModel>
{
    private readonly CompetitionParentContext _parentContext;

    public PhaseBehind(IRepository<Phase> phase, CompetitionParentContext parentContext) : base(phase, parentContext)
    {
        _parentContext = parentContext;
    }

    protected override Phase CreateEntity(PhaseFormModel model)
    {
        return Phase.Create(model.Loop, model.Recovery, model.Rest);
    }

    protected override Phase UpdateEntity(PhaseFormModel model)
    {
        return Phase.Update(model.Id, model.Loop, model.Recovery, model.Rest);
    }
}
