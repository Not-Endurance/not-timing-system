using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Competitions;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class CompetitionBehind : CrudChildBehind<Competition, CompetitionFormModel>
{
    readonly ICrudParent<Phase> _phaseParent;
    readonly ICrudParent<Participation> _participationParent;

    public CompetitionBehind(
        IRepository<Competition> competitions,
        EnduranceEventCrudeContext crudeContext,
        ICrudParent<Phase> phaseParent,
        ICrudParent<Participation> participationParent,
        IEnumerable<ICrudReflection<Competition>> dependants
    )
        : base(competitions, dependants, crudeContext)
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
            model.CompulsoryThresholdMinutes,
            model.FeiRule,
            model.FeiEventCode,
            model.FeiScheduleNumber,
            model.FeiCategoryEventNumber
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
            model.FeiRule,
            model.FeiEventCode,
            model.FeiScheduleNumber,
            model.FeiCategoryEventNumber,
            _phaseParent.Children,
            _participationParent.Children
        );
    }
}
