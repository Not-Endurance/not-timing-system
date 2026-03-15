using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Watcher;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots;

public class SnapshotHistoryBehind : NStatefulComponent
{
    int? _expandedIndex;
    int _historyCount = -1;
    int? _resendingIndex;

    [Inject]
    protected INotifier Notifier { get; set; } = default!;

    [Inject]
    protected INtsSocketContext SocketContext { get; set; } = default!;

    [Inject]
    protected ISnapshotService SnapshotService { get; set; } = default!;

    protected IReadOnlyList<SnapshotGroup> History => [.. SnapshotService.History.Reverse()];
    protected bool CanResend => SocketContext.IsConnected && SocketContext.Event != null;
    protected bool IsResending => _resendingIndex.HasValue;

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

    protected async Task ResendGroup(int index)
    {
        try
        {
            if (_resendingIndex.HasValue || !CanResend || index < 0 || index >= History.Count)
            {
                return;
            }

            _resendingIndex = index;
            var group = History[index];
            await SnapshotService.RePublish(group);
            Notifier.Success(string.Format(Snapshots_sent_as__string, group.Type));
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            _resendingIndex = null;
            StateHasChanged();
        }
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
