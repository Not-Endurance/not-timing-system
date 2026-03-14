using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Application.Watcher;
using NTS.Domain.Watcher;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots;

public class SnapshotHistoryBehind : NStatefulComponent
{
    int? _expandedIndex;
    int _historyCount = -1;
    int? _resendingIndex;

    [Inject]
    protected ISnapshotService SnapshotService { get; set; } = default!;

    [Inject]
    protected INotifier Notifier { get; set; } = default!;

    [Inject]
    protected INtsSocketContext SocketContext { get; set; } = default!;

    [Inject]
    protected IParticipationService ParticipationService { get; set; } = default!;

    protected List<SnapshotHistoryGroupItem> SnapshotGroups { get; set; } = [];
    protected bool CanResend => SocketContext.IsConnected && SocketContext.Event != null;
    protected bool IsResending => _resendingIndex.HasValue;

    protected override async Task OnInitializedAsync()
    {
        await Observe(ParticipationService);
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
            if (_resendingIndex.HasValue || !CanResend || index < 0 || index >= SnapshotGroups.Count)
            {
                return;
            }

            _resendingIndex = index;
            var group = SnapshotGroups[index].Group;
            await SnapshotService.PublishSnapshotsAsync(SnapshotGroupModel.MapFrom(group));
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
        if (_historyCount != ParticipationService.History.Count)
        {
            _historyCount = ParticipationService.History.Count;
            _expandedIndex = null;
        }

        SnapshotGroups =
        [
            .. ParticipationService.History
                .Select(group => new SnapshotHistoryGroupItem(group, group.Entries.ToList()))
                .Reverse() ?? []
        ];
    }

    protected sealed class SnapshotHistoryGroupItem
    {
        public SnapshotHistoryGroupItem(SnapshotGroup group, IReadOnlyList<Snapshot> entries)
        {
            Group = group;
            Entries = entries;
        }

        public SnapshotGroup Group { get; }
        public IReadOnlyList<Snapshot> Entries { get; }
    }
}
