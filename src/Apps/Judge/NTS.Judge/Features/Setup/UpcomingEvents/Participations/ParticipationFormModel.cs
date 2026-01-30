using Not.Application.Services;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class ParticipationFormModel : IFormModel<Participation>
{
    public int? Id { get; set; }
    public bool IsNotRanked { get; set; }
    public Combination? Combination { get; set; }
    public ParticipationCategory? Category { get; set; }
    public bool IsStartTimeOverriden { get; set; }
    public TimeSpan? StartTimeOverride { get; set; }
    public bool IsMaxSpeedOverriden { get; set; }
    public double? MaxSpeedOverride { get; set; }
    public bool IsMinSpeedOverriden { get; set; }
    public double? MinSpeedOverride { get; set; }

    public void FromEntity(Participation participation)
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
}
