using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Features.Console.Setup;

public record LoopFormModel : KrudFormModel<Loop>
{
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
