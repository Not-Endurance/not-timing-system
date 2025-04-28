using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Participations;

public class ParticipationFormModel : IFormModel<Participation>
{
    TimeSpan? _startTimeOverride;
    double? _maxSpeedOverride;

    public int? Id { get; set; }
    public bool OverrideStartTime { get; set; }
    public TimeSpan? StartTimeOverride
    {
        get => _startTimeOverride;
        set
        {
            OverrideStartTime = value != null;
            _startTimeOverride = value;
        }
    }
    public bool IsNotRanked { get; set; }
    public Combination? Combination { get; set; }
    public bool OverrideMaxSpeed { get; set; }
    public double? MaxSpeedOverride 
    {   
        get => _maxSpeedOverride;
        set
        {
            OverrideMaxSpeed = value != null;
            _maxSpeedOverride = value;
        }
    }

    public void FromEntity(Participation participation)
    {
        Id = participation.Id;
        StartTimeOverride = participation.StartTimeOverride?.LocalDateTime.TimeOfDay;
        IsNotRanked = participation.IsNotRanked;
        Combination = participation.Combination;
        MaxSpeedOverride = participation.MaxSpeedOverride;
    }
}
