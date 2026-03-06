using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public record LoopFormModel : KrudFormModel<Loop>
{
    public LoopFormModel()
    {
#if DEBUG
        Distance = 20;
#endif
    }

    public double? Distance { get; set; }

    protected override Loop MapTo()
    {
        return new(Distance, Id);
    }

    public override void MapFrom(Loop loop)
    {
        Id = loop.Id;
        Distance = loop.Distance;
    }
}
