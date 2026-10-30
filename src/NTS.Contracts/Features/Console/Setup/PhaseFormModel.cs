using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Features.Console.Setup;

public record PhaseFormModel : KrudFormModel<Phase>
{
    public Loop? Loop { get; set; }
    public int? Recovery { get; set; }
    public int? Rest { get; set; }
    public bool IsCompulsoryInspectionRequired { get; set; }

    protected override Phase MapTo()
    {
        return new Phase(Loop, Recovery, Rest, Id, IsCompulsoryInspectionRequired);
    }

    public override void MapFrom(Phase phase)
    {
        Id = phase.Id;
        Loop = phase.Loop;
        Recovery = phase.Recovery!;
        Rest = phase.Rest!;
        IsCompulsoryInspectionRequired = phase.IsCompulsoryInspectionRequired;
    }
}
