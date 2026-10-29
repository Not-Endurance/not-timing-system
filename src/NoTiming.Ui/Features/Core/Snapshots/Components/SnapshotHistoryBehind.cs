using MudBlazor;
using Not.Blazor.Components.Buttons;
using NTS.Contracts.Core;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

public class SnapshotHistoryBehind : SnapshotBehindBase
{
    int? _expandedIndex;
    int _historyCount = -1;
    bool _isResending;

    [CascadingParameter]
    IViewedEvent View { get; set; } = default!;

    [Inject]
    protected INtsSocketContext SocketContext { get; set; } = default!;

    protected IReadOnlyList<SnapshotGroup> History => [.. SnapshotService.History.Reverse()];
    protected bool CanResend => View.CanWrite && SocketContext.IsConnected && SocketContext.Event != null;
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
        if (_isResending || !CanResend)
        {
            return;
        }

        _isResending = true;
        await SendAsync(
            async () =>
            {
                await SnapshotService.RePublish(View, group, snapshotType);
                return true;
            },
            snapshotType
        );
        _isResending = false;
        StateHasChanged();
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
