using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Buttons;
using Not.Notify;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Watcher;
using NTS.Witness.Blazor.Features.Socket;
using NTS.Witness.Features.Access;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots;

public class SnapshotContentBehind : NStatefulComponent
{
    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Inject]
    ISnapshotService SnapshotState { get; set; } = default!;

    [Inject]
    BlazorSocketService BlazorSocketService { get; set; } = default!;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected ISnapshotService SnapshotService => SnapshotState;
    protected IReadOnlyList<Participation> Participations => SnapshotService.Participations;
    protected IReadOnlyList<Snapshot> Snapshots => SnapshotService.Snapshots;
    protected IReadOnlyList<NMultiButtonDescriptor> PublishDescriptors =>
        [
            new(Arrive_string, () => SendHandler(SnapshotType.Arrive)),
            new(Presentation_string, () => SendHandler(SnapshotType.Present)),
        ];
    protected int CapturedSnapshotsCount => Snapshots.Count(x => x.Timestamp != null);

    protected override async Task OnInitializedAsync()
    {
        await Observe(AccessState);
        await Observe(SnapshotService);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (WitnessAccessPolicy.ShouldRedirectFromSnapshots(AccessState.AccessLevel))
        {
            Navigator.NavigateTo(WitnessAccessPolicy.ResolveSnapshotFallbackRoute());
            return;
        }

        if (firstRender)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await BlazorSocketService.EnsureConnected();
        }
    }

    

    protected async Task SendHandler(SnapshotType snapshotType)
    {
        try
        {
            if (!await SnapshotService.Publish(snapshotType))
            {
                return;
            }

            Notifier.Success(string.Format(Snapshots_sent_as__string, GetSnapshotTypeText(snapshotType)));
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

    protected Task MoveToSnapshot(Participation? participation)
    {
        try
        {
            if (participation != null)
            {
                SnapshotService.MoveToSnapshot(participation);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }

        return Task.CompletedTask;
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
