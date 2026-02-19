using Not.Blazor.Components;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Inspections;

public class InspectionsPanelBehind : NStatefulComponent 
{
    [Inject]
    IInspectionService Service { get; set; } = default!;

    protected bool IsRepresentRequested => Service.IsRepresentRequested;
    protected bool IsInspectionRequested => Service.IsInspectionRequested;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected async Task HandleRepresentChanged(bool isRequested)
    {
        try
        {
            await Service.RequestRepresent(isRequested);
        }
        catch (Exception ex)
        {
            Handle(ex);
            await InvokeRender();
        }
    }

    protected async Task HandleInspectionChanged(bool isRequested)
    {
        try
        {
            await Service.RequestInspection(isRequested);
        }
        catch (Exception ex)
        {
            Handle(ex);
            await InvokeRender();
        }
    }
}
