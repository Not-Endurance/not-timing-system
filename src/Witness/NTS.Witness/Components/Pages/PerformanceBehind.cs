using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Witness.Components.Pages;

public class PerformanceBehind : ComponentBase
{
    protected List<NTS.Domain.Core.Aggregates.Participations.Phase> _phases = [];
    protected PhaseCollection? _phasesCollection;
    protected Person? _participant;

    protected override void OnInitialized()
    {
        _participant = new Person(["Todomir", "Stroinov"]);
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
        _phases.Add(phase1);
        _phases.Add(phase2);
        _phasesCollection = new(_phases);
    }
}
