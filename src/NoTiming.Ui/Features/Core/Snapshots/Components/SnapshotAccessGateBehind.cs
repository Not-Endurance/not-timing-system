using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Core;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

/// <summary>
/// Sends a person who may not send Snapshots to the Event away from the Snapshot page (#645): to its Performance page,
/// which anybody has. It asks the viewed Event, which is opened after the connection has asked the Api what the person may
/// do, and not the access context on its own, which is not settled for the Event until then; and it asks again when that
/// changes, so that a person whose access was removed loses the page at the next request that says so.
/// </summary>
public class SnapshotAccessGateBehind : NStatefulComponent
{
    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [CascadingParameter]
    IViewedEvent View { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(View);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!View.CanWrite)
        {
            Navigator.NavigateTo(Routes.Of(Routes.EVENT_PERFORMANCE_PAGE, View.Event.Id), replace: true);
        }
    }
}
