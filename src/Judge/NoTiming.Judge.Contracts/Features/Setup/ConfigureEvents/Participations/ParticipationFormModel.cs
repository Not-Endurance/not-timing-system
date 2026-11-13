using NC.DateAndTime;
using NC.Domain.Exceptions;
using NC.Krud.Models;
using NoTiming.Application.Contracts.Shared.Models;
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
    public SpeedRestrictionsModel Restrictions { get; set; } = new();

    protected override Participation MapTo()
    {
        ValidateStartTimeInput();
        var newStart = OverrideStartTime();
        return new(IsNotRanked, Combination, Category, newStart, Restrictions.ToDomain(), id: Id);
    }

    public override void MapFrom(Participation participation)
    {
        Id = participation.Id;
        IsNotRanked = participation.IsNotRanked;
        Combination = participation.Combination;
        Category = participation.Category;
        IsStartTimeOverriden = participation.StartTimeOverride != null;
        StartTimeOverride = participation.StartTimeOverride?.LocalDateTime.TimeOfDay;
        Restrictions = SpeedRestrictionsModel.From(participation);
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
