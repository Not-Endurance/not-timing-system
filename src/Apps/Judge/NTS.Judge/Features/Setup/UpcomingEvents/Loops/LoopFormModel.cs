using Not.Application.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopFormModel : IFormModel<Loop>
{
    public LoopFormModel()
    {
#if DEBUG
        Distance = 20;
#endif
    }

    public double? Distance { get; set; }

    public void FromEntity(Loop entity)
    {
        Distance = entity.Distance;
    }
}
