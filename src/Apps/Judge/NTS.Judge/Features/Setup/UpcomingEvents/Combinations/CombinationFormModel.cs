using Not.Application.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationFormModel : IFormModel<Combination>
{
    public CombinationFormModel()
    {
#if DEBUG
        Number = 37;
#endif
    }

    public int? Number { get; set; }
    public Athlete? Athlete { get; set; }
    public Horse? Horse { get; set; }

    public void FromEntity(Combination combination)
    {
        Number = combination.Number;
        Athlete = combination.Athlete;
        Horse = combination.Horse;
    }
}
