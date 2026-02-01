using Not.DateAndTime;
using Not.Domain.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class ParticipationFormModel : KrudFormModel<Participation>
{
    public bool IsNotRanked { get; set; }
    public Combination? Combination { get; set; }
    public ParticipationCategory? Category { get; set; }
    public bool IsStartTimeOverriden { get; set; }
    public TimeSpan? StartTimeOverride { get; set; }
    public bool IsMaxSpeedOverriden { get; set; }
    public double? MaxSpeedOverride { get; set; }
    public bool IsMinSpeedOverriden { get; set; }
    public double? MinSpeedOverride { get; set; }

    protected override Participation MapTo()
    {
        ValidateOverrideInput(this);
        var newStart = OverrideStartTime(this);
        return new(IsNotRanked, Combination, Category, newStart, MaxSpeedOverride, MinSpeedOverride, Id);
    }

    public override void MapFrom(Participation participation)
    {
        Id = participation.Id;
        IsNotRanked = participation.IsNotRanked;
        Combination = participation.Combination;
        Category = participation.Category;
        IsStartTimeOverriden = participation.StartTimeOverride != null;
        StartTimeOverride = participation.StartTimeOverride?.LocalDateTime.TimeOfDay;
        IsMaxSpeedOverriden = participation.MaxSpeedOverride != null;
        MaxSpeedOverride = participation.MaxSpeedOverride;
        IsMinSpeedOverriden = participation.MinSpeedOverride != null;
        MinSpeedOverride = participation.MinSpeedOverride;
    }


    void ValidateOverrideInput(ParticipationFormModel model)
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

    DateTimeOffset? OverrideStartTime(ParticipationFormModel model)
    {
        if (model.StartTimeOverride == null)
        {
            return null;
        }
        return model.StartTimeOverride.Value.ToTodayDateTime().ToLocalDateTime();
    }
}
