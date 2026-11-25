using NC.Application.HTTP;
using NC.Exceptions;
using NC.Injection;
using NC.Structures;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Watcher;

namespace NoTiming.Ui.Application.Features.Core.Dashboard;

/// <summary>Sends a witness's snapshots to the API, which applies them and announces the changes.</summary>
public class UiSnapshotPublisher : ISnapshotPublisher, IScoped
{
    readonly NHttpClient _client;
    readonly INoTimingSocketContext _socketContext;

    public UiSnapshotPublisher(NHttpClient client, INoTimingSocketContext socketContext)
    {
        _client = client;
        _socketContext = socketContext;
    }

    public async Task PublishSnapshotsAsync(SnapshotGroup snapshotGroup)
    {
        var connectedEvent = GuardHelper.ThrowIfDefault(
            _socketContext.Event,
            "Cannot publish witness snapshots before connecting to an event."
        );

        var model = SnapshotGroupModel.MapFrom(snapshotGroup);
        var result = await _client.Post<Result.Empty>($"snapshots/{connectedEvent.Id}", model);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        }
    }
}
