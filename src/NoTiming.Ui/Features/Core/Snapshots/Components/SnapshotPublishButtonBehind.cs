using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Buttons;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

/// <summary>
/// The control that sends the captured Snapshots to the viewed Event (#630, ADR-0007): it is off when the Event cannot be
/// written to, and the service refuses the call as well.
/// </summary>
public class SnapshotPublishButtonBehind : NStatefulComponent
{
    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Inject]
    ISnapshotService SnapshotService { get; set; } = default!;

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
        try
        {
            if (!await SnapshotService.Publish(View, snapshotType))
            {
                return;
            }

            Notifier.Success(string.Format(Snapshots_sent_as__string, GetSnapshotTypeText(snapshotType)));
        }
        catch (Exception ex) when (ex is not DomainException && SnapshotService.Unanswered != null)
        {
            // No answer came: the group is kept, and the app sends it again until the server answers.
            Notifier.Warn(Snapshots_kept_to_be_sent_again_string);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            StateHasChanged();
        }
    }

    protected string GetSnapshotTypeText(SnapshotType snapshotType)
    {
        return snapshotType switch
        {
            SnapshotType.Arrive => Arrive_string,
            SnapshotType.Present => Presentation_string,
            _ => snapshotType.ToString(),
        };
    }
}
