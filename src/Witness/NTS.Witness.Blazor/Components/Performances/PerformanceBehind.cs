using Not.Blazor.Components;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Witness.Blazor.Components.Performances;

public class PerformanceBehind : NComponent
{
    [Inject]
    IPerformanceService Service { get; set; } = default!;

    protected List<Phase> Phases { get; set; } = [];
    protected PhaseCollection? PhasesCollection { get; set; }
    protected Person? Participant { get; set; }

    protected override void OnInitialized()
    {
        try
        {
            Participant = Service.GetPerson();
            Phases = Service.GetPhases();
            PhasesCollection = new(Phases);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
