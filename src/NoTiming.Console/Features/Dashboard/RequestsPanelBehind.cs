using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Dashboard;

/// <summary>
/// The Representation and the Inspection that are requested for the Participation (#646): they describe the Phase that a
/// request made now would act on, by the time of the server, and a request the Api does not take leaves them as they were.
/// </summary>
public class RequestsPanelBehind : NStatefulComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected bool IsRepresentRequested => Dashboard.IsRepresentRequested;
    protected bool IsInspectionRequested => Dashboard.IsInspectionRequested;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Dashboard);
    }

    protected Task RequestRepresentation(bool isRequested)
    {
        return Request(() => Dashboard.RequestRepresentation(isRequested));
    }

    protected Task RequestInspection(bool isRequested)
    {
        return Request(() => Dashboard.RequestInspection(isRequested));
    }

    async Task Request(Func<Task> request)
    {
        try
        {
            await request();
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            await InvokeRender();
        }
    }
}
