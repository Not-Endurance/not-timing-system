using NC.DateAndTime;
using NC.Domain.Exceptions;
using NC.Krud.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Participations;

public record ParticipationFormModel : KrudFormModel<Participation>
{
    public bool IsNotRanked { get; set; }
    public Combination? Combination { get; set; }
    public ParticipationCategory? Category { get; set; }
    public bool IsStartTimeOverriden { get; set; }
    public TimeSpan? StartTimeOverride { get; set; }
    public double? LoopSpeedMin { get; set; }
    public double? LoopSpeedMax { get; set; }
    public double? PhaseSpeedMin { get; set; }
    public double? PhaseSpeedMax { get; set; }
    public double? LoopAverageSpeedMin { get; set; }
    public double? LoopAverageSpeedMax { get; set; }
    public double? PhaseAverageSpeedMin { get; set; }
    public double? PhaseAverageSpeedMax { get; set; }

    protected override Participation MapTo()
    {
        ValidateStartTimeInput();
        var newStart = OverrideStartTime();
        return new(
            IsNotRanked,
            Combination,
            Category,
            newStart,
            new SpeedRestrictions(
                LoopSpeedMin,
                LoopSpeedMax,
                PhaseSpeedMin,
                PhaseSpeedMax,
                LoopAverageSpeedMin,
                LoopAverageSpeedMax,
                PhaseAverageSpeedMin,
                PhaseAverageSpeedMax
            ),
            id: Id
        );
    }

    public override void MapFrom(Participation participation)
    {
        Id = participation.Id;
        IsNotRanked = participation.IsNotRanked;
        Combination = participation.Combination;
        Category = participation.Category;
        IsStartTimeOverriden = participation.StartTimeOverride != null;
        StartTimeOverride = participation.StartTimeOverride?.LocalDateTime.TimeOfDay;
        var restrictions = participation.Restrictions;
        LoopSpeedMin = restrictions.LoopSpeedMin;
        LoopSpeedMax = restrictions.LoopSpeedMax;
        PhaseSpeedMin = restrictions.PhaseSpeedMin;
        PhaseSpeedMax = restrictions.PhaseSpeedMax;
        LoopAverageSpeedMin = restrictions.LoopAverageSpeedMin;
        LoopAverageSpeedMax = restrictions.LoopAverageSpeedMax;
        PhaseAverageSpeedMin = restrictions.PhaseAverageSpeedMin;
        PhaseAverageSpeedMax = restrictions.PhaseAverageSpeedMax;
    }

    void ValidateStartTimeInput()
    {
        if (IsStartTimeOverriden && StartTimeOverride == null)
        {
            throw new DomainPropertyException(nameof(StartTimeOverride), Null_or_malformed_string, Start_Time_string);
        }
    }

    DateTimeOffset? OverrideStartTime()
    {
        if (!IsStartTimeOverriden || StartTimeOverride == null)
        {
            return null;
        }
        return DateTime.Today.ToLocalDateTime(StartTimeOverride.Value);
    }
}
