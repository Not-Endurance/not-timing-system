using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Inspections;

public class InspectionsPanelBehind : NStatefulComponent<IInspectionService>
{
    protected Participation? SelectedParticipation => Service.SelectedParticipation;
    protected Phase? CurrentPhase => SelectedParticipation?.Phases.Current;
    protected bool Represent => CurrentPhase?.IsReinspectionRequested ?? false;
    protected bool RequireInspection => CurrentPhase?.IsRequiredInspectionRequested ?? false;

    protected async Task ToggleReinspection(bool value)
    {
        await Service.RequestRepresent(value);
    }

    protected async Task ToggleRequiredInspection(bool value)
    {
        await Service.RequireRepresent(value);
    }
}
