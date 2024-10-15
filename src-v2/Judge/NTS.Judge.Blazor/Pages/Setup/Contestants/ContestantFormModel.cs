using Not.Blazor.Ports;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Contestants;

public class ContestantFormModel : IFormModel<Contestant>
{
    public ContestantFormModel()
    {
        // mock data for testing
        StartTimeOverride = null;
    }

    public int Id { get; set; }
    public TimeSpan? StartTimeOverride { get; set; }
    public Boolean  IsUnranked { get; set; }
    public Combination? Combination { get; set; }

    public void FromEntity(Contestant contestant)
    {
        Id = contestant.Id;
        StartTimeOverride = contestant.StartTimeOverride?.LocalDateTime.TimeOfDay;
        IsUnranked = contestant.IsUnranked;
        Combination = contestant.Combination;
    }
}
