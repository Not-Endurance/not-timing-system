using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

/// <summary>
/// The send that the Snapshot controls share: it says what was sent, keeps a group that no answer came to for sending
/// again (#645, ADR-0013) and shows any other failure. The control re-renders itself once it has settled its own state.
/// </summary>
public abstract class SnapshotBehindBase : NStatefulComponent
{
    [Inject]
    protected INotifier Notifier { get; set; } = default!;

    [Inject]
    protected ISnapshotService SnapshotService { get; set; } = default!;

    /// <summary>
    /// Runs <paramref name="send"/>, which returns false when nothing was sent and then nothing is said.
    /// </summary>
    protected async Task SendAsync(Func<Task<bool>> send, SnapshotType snapshotType)
    {
        try
        {
            if (!await send())
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
