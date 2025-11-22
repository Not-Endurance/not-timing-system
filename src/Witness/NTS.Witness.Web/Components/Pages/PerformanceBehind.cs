using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Phase;

namespace NTS.Witness.Web.Components.Pages;

public class PerformanceBehind : NComponent
{
    protected List<CorePhase> Phases { get; set; } = [];
    protected PhaseCollection? PhasesCollection { get; set; }
    protected Person? Participant { get; set; }

    protected override void OnInitialized()
    {
        try
        {
            Participant = DummyData.CreateParticipant("Todomir", "Stroinov");
            Phases = DummyData.CreatePhases();
            PhasesCollection = new(Phases);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
