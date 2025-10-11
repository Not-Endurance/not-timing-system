using Not.Safe;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Witness.Components.Pages;

public class PerformanceBehind : ComponentBase
{
    protected List<NTS.Domain.Core.Aggregates.Participations.Phase> Phases { get; set; } = [];
    protected PhaseCollection? PhasesCollection { get; set; }
    protected Person? Participant { get; set; }

    protected override void OnInitialized()
    {
        try
        {
            Participant = new Person(["Todomir", "Stroinov"]);
            var phase1 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                20,
                15,
                40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                false,
                null,
                DateTimeOffset.Now.AddMinutes(23)
            );
            var phase2 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                20,
                20,
                40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                true,
                null,
                null
            );
            Phases.Add(phase1);
            Phases.Add(phase2);
            PhasesCollection = new(Phases);
        }
        catch (Exception ex)
        {
            SafeHelper.HandleError(ex);
        }
    }
}
