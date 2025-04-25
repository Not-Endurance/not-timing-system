using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Participations;

public class ParticipationFormModel : IFormModel<Participation>
{
    public int? Id { get; set; }
    public TimeSpan? StartTimeOverride { get; set; } // TODO: toggle checkbox
    public bool IsNotRanked { get; set; }
    public Combination? Combination { get; set; }
    public double? MaxSpeedOverride { get; set; } // TODO: toggle checkbox

    public void FromEntity(Participation participation)
    {
        Id = participation.Id;
        StartTimeOverride = participation.StartTimeOverride?.LocalDateTime.TimeOfDay;
        IsNotRanked = participation.IsNotRanked;
        Combination = participation.Combination;
        MaxSpeedOverride = participation.MaxSpeedOverride;
    }
}
