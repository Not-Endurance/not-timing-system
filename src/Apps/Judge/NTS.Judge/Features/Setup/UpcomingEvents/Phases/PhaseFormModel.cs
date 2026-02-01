using Not.Krud.Abstractions;
using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseFormModel : KrudFormModel<Phase>
{
    public PhaseFormModel()
    {
#if DEBUG
        Recovery = 15;
        Rest = 40;
#endif
    }

    public Loop? Loop { get; set; }
    public int? Recovery { get; set; }
    public int? Rest { get; set; }

    protected override Phase MapTo()
    {
        return new Phase(Loop, Recovery, Rest, Id);
    }

    public override void MapFrom(Phase phase)
    {
        Id = phase.Id;
        Loop = phase.Loop;
        Recovery = phase.Recovery!;
        Rest = phase.Rest!;
    }
}
