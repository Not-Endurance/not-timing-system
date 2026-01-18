using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using Not.Domain.Exceptions;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class SetupParticipationBehind : KrudServiceBase<Participation, ParticipationFormModel>
{
    public SetupParticipationBehind(
        IRepository<Participation> participations,
        IEnumerable<IKrudMirror<Participation>> dependants
    )
        : base(participations, dependants) { }

    protected override Participation CreateEntity(ParticipationFormModel model)
    {
        ValidateEntity(model);
        var newStart = OverrideStartTime(model);
        return new(
            model.IsNotRanked,
            model.Combination,
            model.Category,
            newStart,
            model.MaxSpeedOverride,
            model.MinSpeedOverride
        );
    }

    public void ValidateEntity(ParticipationFormModel model)
    {
        if (model.IsStartTimeOverriden && model.StartTimeOverride == null)
        {
            throw new DomainPropertyException(
                nameof(model.StartTimeOverride),
                Null_or_malformed_string,
                Start_Time_string
            );
        }
        if (model.IsMaxSpeedOverriden && model.MaxSpeedOverride == null)
        {
            throw new DomainPropertyException(
                nameof(model.MaxSpeedOverride),
                Null_or_malformed_string,
                Max_Speed_string
            );
        }
        if (model.IsMinSpeedOverriden && model.MinSpeedOverride == null)
        {
            throw new DomainPropertyException(
                nameof(model.MinSpeedOverride),
                Null_or_malformed_string,
                Min_Speed_string
            );
        }
    }

    public DateTimeOffset? OverrideStartTime(ParticipationFormModel model)
    {
        if (model.StartTimeOverride != null)
        {
            var time = (TimeSpan)model.StartTimeOverride;
            return DateTime.Today.Date.Add(time).ToDateTimeOffset();
        }
        return null;
    }
}
