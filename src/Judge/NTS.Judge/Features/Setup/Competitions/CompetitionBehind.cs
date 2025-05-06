using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Competitions;
using NTS.Judge.Core.Behinds;
using Not.Extensions;

namespace NTS.Judge.Features.Setup.Competitions;

public class CompetitionBehind : CrudChildBehind<Competition, CompetitionFormModel>
{
    readonly ICrudeParent<Phase> _phaseParent;
    readonly ICrudeParent<Participation> _participationParent;

    public CompetitionBehind(
        UpcomingEventCrudeContext crudeContext,
        ICrudeParent<Phase> phaseParent,
        ICrudeParent<Participation> participationParent,
        IEnumerable<ICrudReflection<Competition>> dependants
    )
        : base(dependants, crudeContext)
    {
        _phaseParent = phaseParent;
        _participationParent = participationParent;
    }

    protected override Competition CreateEntity(CompetitionFormModel model)
    {
        ValidateDateTime(model);
        var date = (DateTime)model.Date!;
        var startTime = date.ToDateTimeOffset().Add((TimeSpan)model.Time!);
        return Competition.Create(
            model.Name,
            model.Type,
            model.Ruleset,
            startTime,
            model.CompulsoryThresholdMinutes,
            model.FeiRule,
            model.FeiEventCode,
            model.FeiScheduleNumber,
            model.FeiCategoryEventNumber
        );
    }

    protected override Competition UpdateEntity(CompetitionFormModel model)
    {
        ValidateDateTime(model);
        var date = (DateTime)model.Date!;
        var startTime = date.ToDateTimeOffset().Add((TimeSpan)model.Time!);
        return Competition.Update(
            model.Id,
            model.Name,
            model.Type,
            model.Ruleset,
            startTime,
            model.CompulsoryThresholdMinutes,
            model.FeiRule,
            model.FeiEventCode,
            model.FeiScheduleNumber,
            model.FeiCategoryEventNumber,
            _phaseParent.Children,
            _participationParent.Children
        );
    }

    void ValidateDateTime(CompetitionFormModel model)
    {
        if (model.Date == null)
        {
            throw new DomainPropertyException(nameof(CompetitionFormModel.Date), Null_or_malformed_string, Time_string);
        }
        if (model.Time == null)
        {
            throw new DomainPropertyException(nameof(CompetitionFormModel.Time), Null_or_malformed_string, Time_string);
        }
    }
}
