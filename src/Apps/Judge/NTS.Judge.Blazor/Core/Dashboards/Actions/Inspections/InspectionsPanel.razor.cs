using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Inspections;

public partial class InspectionsPanel
{
    [Inject]
    IInspections Inspections { get; set; } = default!;

    Participation? SelectedParticipation => Inspections.SelectedParticipation; // TODO: fix naming rules should be _ prefixed
    Phase? CurrentPhase => SelectedParticipation?.Phases.Current;
    bool Represent => CurrentPhase?.IsReinspectionRequested ?? false;
    bool RequireInspection => CurrentPhase?.IsRequiredInspectionRequested ?? false;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Inspections);
    }

    async Task ToggleReinspection(bool value)
    {
        await Inspections.RequestRepresent(value);
    }

    async Task ToggleRequiredInspection(bool value)
    {
        await Inspections.RequireInspection(value);
    }
}
