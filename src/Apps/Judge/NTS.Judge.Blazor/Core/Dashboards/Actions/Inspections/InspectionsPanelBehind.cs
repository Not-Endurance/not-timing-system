using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Inspections;

public class InspectionsPanelBehind : NStatefulComponent<IInspectionService>
{
    public InspectionsPanelBehind()
    {
        ToggleReinspection = EventCallback.Factory.Create<bool>(this, Service.RequestRepresent);
        ToggleRequiredInspection = EventCallback.Factory.Create<bool>(this, Service.RequireRepresent);
    }
    protected EventCallback<bool> ToggleReinspection { get; }
    protected EventCallback<bool> ToggleRequiredInspection { get; }
    protected Participation? SelectedParticipation => Service.SelectedParticipation;
    protected Phase? CurrentPhase => SelectedParticipation?.Phases.Current;
    protected bool Represent => CurrentPhase?.IsReinspectionRequested ?? false;
    protected bool RequireInspection => CurrentPhase?.IsRequiredInspectionRequested ?? false;
}
