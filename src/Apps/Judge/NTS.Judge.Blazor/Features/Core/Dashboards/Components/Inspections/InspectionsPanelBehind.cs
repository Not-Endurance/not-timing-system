using Not.Blazor.Components;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Inspections;

public class InspectionsPanelBehind : NStatefulComponent
{
    [Inject]
    IInspectionService Service { get; set; } = default!;

    protected EventCallback<bool> ToggleRequested { get; private set; }
    protected EventCallback<bool> ToggleRequired { get; private set; }
    protected bool IsRepresentRequired => Service.IsRepresentRequired;
    protected bool IsRepresentRequested => Service.IsRepresentRequested;

    protected override void OnInitialized()
    {
        ToggleRequested = EventCallback.Factory.Create<bool>(this, Service.RequestRepresent);
        ToggleRequired = EventCallback.Factory.Create<bool>(this, Service.RequireRepresent);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
