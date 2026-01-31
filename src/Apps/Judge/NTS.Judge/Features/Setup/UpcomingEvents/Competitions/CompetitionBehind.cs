using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Extensions;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionBehind : KrudServiceBase<Competition, CompetitionFormModel>
{
    readonly IKrudParentNodeOf<Phase> _phaseParent;
    readonly IKrudParentNodeOf<Participation> _participationParent;

    public CompetitionBehind(
        IKrudParentNodeOf<Phase> phaseParent,
        IKrudParentNodeOf<Participation> participationParent,
        IRepository<Competition> competitions
    )
        : base(competitions, [])
    {
        _phaseParent = phaseParent;
        _participationParent = participationParent;
    }

    protected override Competition CreateEntity(CompetitionFormModel model)
    {
        var startTime = ConvertStartTime(model.Date, model.Time);
        var compulsoryThreshold = ConvertMinutes(model.CompulsoryThresholdMinutes);
        return new Competition(
            model.Name,
            model.Type,
            model.Ruleset,
            startTime,
            compulsoryThreshold,
            model.FeiId,
            model.FeiRule,
            model.FeiScheduleNumber,
            _phaseParent.Children,
            _participationParent.Children,
            model.Id
        );
    }

    TimeSpan? ConvertMinutes(int? minutes)
    {
        return minutes == null ? null : TimeSpan.FromMinutes(minutes.Value);
    }

    DateTimeOffset ConvertStartTime(DateTime? date, TimeSpan? time)
    {
        if (date == null)
        {
            throw new DomainPropertyException(nameof(CompetitionFormModel.Date), Null_or_malformed_string, Time_string);
        }
        if (time == null)
        {
            throw new DomainPropertyException(nameof(CompetitionFormModel.Time), Null_or_malformed_string, Time_string);
        }

        return date.Value.ToDateTimeOffset().Add(time.Value);
    }
}
