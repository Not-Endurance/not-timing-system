using Not.DateAndTime;
using Not.Domain.Exceptions;
using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Participations;

public record ParticipationFormModel : KrudFormModel<Participation>
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
        ValidateOverrideInput();
        var newStart = OverrideStartTime();
        var maxSpeedOverride = OverrideMaxSpeed();
        var minSpeedOverride = OverrideMinSpeed();
        return new(IsNotRanked, Combination, Category, newStart, maxSpeedOverride, minSpeedOverride, id: Id);
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

    void ValidateOverrideInput()
    {
        if (IsStartTimeOverriden && StartTimeOverride == null)
        {
            throw new DomainPropertyException(nameof(StartTimeOverride), Null_or_malformed_string, Start_Time_string);
        }
        if (IsMaxSpeedOverriden && MaxSpeedOverride == null)
        {
            throw new DomainPropertyException(nameof(MaxSpeedOverride), Null_or_malformed_string, Max_Speed_string);
        }
        if (IsMinSpeedOverriden && MinSpeedOverride == null)
        {
            throw new DomainPropertyException(nameof(MinSpeedOverride), Null_or_malformed_string, Min_Speed_string);
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

    double? OverrideMaxSpeed()
    {
        return IsMaxSpeedOverriden ? MaxSpeedOverride : null;
    }

    double? OverrideMinSpeed()
    {
        return IsMinSpeedOverriden ? MinSpeedOverride : null;
    }
}
