using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Enums;
using NTS.Domain.Watcher;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots.Components;

public class SnapshotHistoryBehind : NStatefulComponent
{
    readonly Dictionary<int, SnapshotType> _selectedResendTypes = [];
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

    protected SnapshotType GetSelectedResendType(SnapshotGroup group)
    {
        if (_selectedResendTypes.TryGetValue(group.Id, out var snapshotType))
        {
            return snapshotType;
        }

        return group.Type switch
        {
            SnapshotType.Arrive => SnapshotType.Arrive,
            SnapshotType.Present => SnapshotType.Present,
            _ => SnapshotType.Arrive,
        };
    }

    protected void SetResendType(SnapshotGroup group, SnapshotType snapshotType)
    {
        try
        {
            _selectedResendTypes[group.Id] = snapshotType;
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
            var snapshotType = GetSelectedResendType(group);
            await SnapshotService.RePublish(group, snapshotType);
            Notifier.Success(string.Format(Snapshots_sent_as__string, GetSnapshotTypeText(snapshotType)));
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

    protected string GetSnapshotTypeText(SnapshotType snapshotType)
    {
        return snapshotType switch
        {
            SnapshotType.Arrive => Arrive_string,
            SnapshotType.Present => Presentation_string,
            _ => snapshotType.ToString(),
        };
    }

    protected string GetResendButtonText(SnapshotGroup group)
    {
        return $"{Resend_string}: {GetSnapshotTypeText(GetSelectedResendType(group))}";
    }

    protected override void OnBeforeRender()
    {
        var historyIds = SnapshotService.History.Select(x => x.Id).ToHashSet();
        foreach (var id in _selectedResendTypes.Keys.Where(id => !historyIds.Contains(id)).ToArray())
        {
            _selectedResendTypes.Remove(id);
        }

        if (_historyCount != SnapshotService.History.Count)
        {
            _historyCount = SnapshotService.History.Count;
            _expandedIndex = null;
        }
    }
}
