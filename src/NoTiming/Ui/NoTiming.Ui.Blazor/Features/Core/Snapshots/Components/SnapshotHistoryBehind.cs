using MudBlazor;
using NC.Blazor.Components.Abstractions;
using NC.Blazor.Components.Buttons;
using NC.Notify;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Watcher;
using NoTiming.Ui.Contracts.Features.Snapshots;

namespace NoTiming.Ui.Blazor.Features.Core.Snapshots.Components;

public class SnapshotHistoryBehind : NStatefulComponent
{
    int? _expandedIndex;
    int _historyCount = -1;
    bool _isResending;

    [Inject]
    protected INotifier Notifier { get; set; } = default!;

    [Inject]
    protected INoTimingSocketContext SocketContext { get; set; } = default!;

    [Inject]
    protected ISnapshotService SnapshotService { get; set; } = default!;

    protected IReadOnlyList<SnapshotGroup> History => [.. SnapshotService.History.Reverse()];
    protected bool CanResend => SocketContext.IsConnected && SocketContext.Event != null;
    protected bool IsResending => _isResending;

    protected override async Task OnInitializedAsync()
    {
        await Observe(SnapshotService);
    }

    protected bool IsExpanded(int index)
    {
        return _expandedIndex == index;
    }

    protected void ToggleGroup(int index)
    {
        try
        {
            if (_expandedIndex == index)
            {
                _expandedIndex = null;
                return;
            }

            _expandedIndex = index;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected IReadOnlyList<NDropdownButtonDescriptor> GetResendDescriptors(SnapshotGroup group)
    {
        var selectedType = group.Type switch
        {
            SnapshotType.Present => SnapshotType.Present,
            _ => SnapshotType.Arrive,
        };
        var alternateType = selectedType == SnapshotType.Arrive ? SnapshotType.Present : SnapshotType.Arrive;

        return [CreateDescriptor(group, selectedType), CreateDescriptor(group, alternateType)];

        NDropdownButtonDescriptor CreateDescriptor(SnapshotGroup snapshotGroup, SnapshotType snapshotType)
        {
            return new(
                $"{Resend_string}: {GetSnapshotTypeText(snapshotType)}",
                () => ResendGroup(snapshotGroup, snapshotType),
                Icons.Material.Filled.Replay
            );
        }
    }

    protected async Task ResendGroup(SnapshotGroup group, SnapshotType snapshotType)
    {
        try
        {
            if (_isResending || !CanResend)
            {
                return;
            }

            _isResending = true;
            await SnapshotService.RePublish(group, snapshotType);
            Notifier.Success(string.Format(Snapshots_sent_as__string, GetSnapshotTypeText(snapshotType)));
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            _isResending = false;
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

    protected override void OnBeforeRender()
    {
        if (_historyCount != SnapshotService.History.Count)
        {
            _historyCount = SnapshotService.History.Count;
            _expandedIndex = null;
        }
    }
}
