using Not.Blazor.Ports;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Contestants;

public class ParticipationFormModel : IFormModel<Participation>
{
    public ParticipationFormModel()
    {
        // mock data for testing
        StartTimeOverride = null;
    }

    public int Id { get; set; }
    public TimeSpan? StartTimeOverride { get; set; }
    public bool IsNotRanked { get; set; }
    public Combination? Combination { get; set; }

    public void FromEntity(Participation participation)
    {
        Id = participation.Id;
        StartTimeOverride = participation.StartTimeOverride?.LocalDateTime.TimeOfDay;
        IsNotRanked = participation.IsNotRanked;
        Combination = participation.Combination;
    }
}
