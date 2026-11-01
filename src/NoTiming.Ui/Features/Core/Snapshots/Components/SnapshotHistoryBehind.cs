using System.Globalization;
using MudBlazor;
using Not.Blazor.Components.Buttons;
using Not.Domain.Exceptions;
using NTS.Contracts.Core;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

public class SnapshotHistoryBehind : SnapshotBehindBase
{
    readonly Dictionary<(Guid GroupId, int Number), PatternMask> _masks = [];
    readonly Dictionary<(Guid GroupId, int Number), string> _typed = [];
    int? _expandedIndex;
    int _historyCount = -1;
    bool _isResending;
    (Guid GroupId, int Number)? _updating;

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

    /// <summary>Whether the time of the entry can be corrected: the person may write, and it is the latest of its start number and kind.</summary>
    protected bool CanUpdate(SnapshotGroup group, Snapshot entry)
    {
        return View.CanWrite && SnapshotService.CanUpdate(group, entry);
    }

    /// <summary>The mask of the field of the entry: a mask is the state of one field, and is not shared by two.</summary>
    protected PatternMask MaskOf(SnapshotGroup group, Snapshot entry)
    {
        var key = (group.Id, entry.Number);
        if (!_masks.TryGetValue(key, out var mask))
        {
            mask = _masks[key] = new PatternMask("00:00:00");
        }

        return mask;
    }

    protected bool IsUpdating(SnapshotGroup group, Snapshot entry)
    {
        return _updating == (group.Id, entry.Number);
    }

    /// <summary>What the field of the entry holds: what the person typed, or the time the entry has.</summary>
    protected string TimeOf(SnapshotGroup group, Snapshot entry)
    {
        return _typed.TryGetValue((group.Id, entry.Number), out var typed)
            ? typed
            : entry.Timestamp?.ToString() ?? string.Empty;
    }

    protected void Typed(SnapshotGroup group, Snapshot entry, string? time)
    {
        _typed[(group.Id, entry.Number)] = time ?? string.Empty;
    }

    /// <summary>
    /// Corrects the time of the entry at the server: the time of the day that was typed on the day the entry has. The entry shows
    /// the new time when the server took it; when it did not, the person is told why and the field is what the entry has again.
    /// A request that no answer came to leaves what was typed, so that the same request is sent again by pressing the button.
    /// </summary>
    protected async Task Update(SnapshotGroup group, Snapshot entry)
    {
        var key = (group.Id, entry.Number);
        if (_updating != null || !CanUpdate(group, entry))
        {
            return;
        }

        if (
            !TimeSpan.TryParseExact(
                TimeOf(group, entry),
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out var timeOfDay
            )
        )
        {
            Notifier.Inform(Time_format_is_incorrect_hrs_colon_mins_colon_secs_string);
            return;
        }

        try
        {
            _updating = key;
            StateHasChanged();
            await SnapshotService.UpdateTime(
                View,
                group,
                entry,
                CorrectedTime.Of(entry.Timestamp!, timeOfDay, TimeZoneInfo.Local)
            );
            _typed.Remove(key);
            Notifier.Success(Snapshot_time_updated_string);
        }
        catch (DomainException ex)
        {
            _typed.Remove(key);
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or TaskCanceledException)
        {
            Notifier.Warn(Snapshot_update_not_answered_string); // what was typed stays, and the button sends the same request again
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            _updating = null;
            StateHasChanged();
        }
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
