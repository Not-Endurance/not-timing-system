using NC.Krud.Models;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Loops;

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
