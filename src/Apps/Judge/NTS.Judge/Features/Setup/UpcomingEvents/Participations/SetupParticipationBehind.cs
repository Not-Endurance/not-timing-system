using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using Not.Domain.Exceptions;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class SetupParticipationBehind : KrudService<Participation, ParticipationFormModel>
{
    public SetupParticipationBehind(
        IKrudParentNodeOf<Participation> crudeContext,
        IEnumerable<IKrudMirror<Participation>> dependants
    )
        : base(dependants, crudeContext) { }

    protected override Participation CreateEntity(ParticipationFormModel model)
    {
        ValidateEntity(model);
        var newStart = OverrideStartTime(model);
        return new(
            model.Id,
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
