using Not.Blazor.Components.Buttons;
using NTS.Contracts.Core;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

/// <summary>
/// The control that sends the captured Snapshots to the viewed Event (#630, ADR-0007): it is off when the Event cannot be
/// written to, and the service refuses the call as well.
/// </summary>
public class SnapshotPublishButtonBehind : SnapshotBehindBase
{
    [CascadingParameter]
    IViewedEvent View { get; set; } = default!;

    protected IReadOnlyList<NDropdownButtonDescriptor> PublishDescriptors =>
        [
            new(Arrive_string, () => SendHandler(SnapshotType.Arrive)),
            new(Presentation_string, () => SendHandler(SnapshotType.Present)),
        ];

    protected int CapturedSnapshotsCount => SnapshotService.Snapshots.Count(x => x.Timestamp != null);
    protected bool CanWrite => View.CanWrite;

    protected override async Task OnInitializedAsync()
    {
        await Observe(SnapshotService);
        await Observe(View);
    }

    protected async Task SendHandler(SnapshotType snapshotType)
    {
        await SendAsync(() => SnapshotService.Publish(View, snapshotType), snapshotType);
        StateHasChanged();
    }
}
