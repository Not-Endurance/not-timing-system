using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using Not.Domain.Exceptions;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionBehind : KrudService<Competition, CompetitionFormModel>
{
    readonly IKrudParentNodeOf<Phase> _phaseParent;
    readonly IKrudParentNodeOf<Participation> _participationParent;

    public CompetitionBehind(
        IKrudParentNodeOf<Competition> crudeContext,
        IKrudParentNodeOf<Phase> phaseParent,
        IKrudParentNodeOf<Participation> participationParent,
        IEnumerable<IKrudMirror<Competition>> dependants
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
        return new Competition(
            model.Name,
            model.Type,
            model.Ruleset,
            startTime,
            model.CompulsoryThresholdMinutes,
            model.FeiId,
            model.FeiRule,
            model.FeiScheduleNumber
        );
    }

    protected override Competition UpdateEntity(CompetitionFormModel model)
    {
        ValidateDateTime(model);
        var date = (DateTime)model.Date!;
        var startTime = date.ToDateTimeOffset().Add((TimeSpan)model.Time!);
        var compulsoryThreshold =
            model.CompulsoryThresholdMinutes != null
                ? TimeSpan.FromMinutes(model.CompulsoryThresholdMinutes.Value)
                : (TimeSpan?)null;
        return new Competition(
            model.Id,
            model.Name,
            model.Type,
            model.Ruleset,
            startTime,
            compulsoryThreshold,
            model.FeiId,
            model.FeiRule,
            model.FeiScheduleNumber,
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
