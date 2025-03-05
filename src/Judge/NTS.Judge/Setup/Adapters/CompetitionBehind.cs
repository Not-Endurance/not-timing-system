using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Competitions;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class CompetitionBehind : CrudBehind<Competition, CompetitionFormModel>
{
    readonly IParentContext<Phase> _phaseParent;
    readonly IParentContext<Participation> _participationParent;

    public CompetitionBehind(
        IRepository<Competition> competitions,
        EventParentContext parentContext,
        IParentContext<Phase> phaseParent,
        IParentContext<Participation> participationParent,
        IEnumerable<ISingleParentContext> singleParentContexts
    )
        : base(competitions, singleParentContexts, parentContext)
    {
        _phaseParent = phaseParent;
        _participationParent = participationParent;
    }

    protected override Competition CreateEntity(CompetitionFormModel model)
    {
        return Competition.Create(
            model.Name,
            model.Type,
            model.Ruleset,
            model.StartTime,
            model.CompulsoryThresholdMinutes
        );
    }

    protected override Competition UpdateEntity(CompetitionFormModel model)
    {
        return Competition.Update(
            model.Id,
            model.Name,
            model.Type,
            model.Ruleset,
            model.StartTime,
            model.CompulsoryThresholdMinutes,
            _phaseParent.Children,
            _participationParent.Children
        );
    }
}
