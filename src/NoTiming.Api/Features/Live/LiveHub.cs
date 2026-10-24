using Microsoft.AspNetCore.SignalR;
using Not.Application.RPC;
using NTS.Contracts.Live;

namespace NoTiming.Api.Features.Live;

/// <summary>
/// The one hub (ADR-0013). Anyone may join an Event's group (ADR-0001) and nothing on it is data: it only sends
/// <see cref="ILiveClientProcedures.ParticipationChanged"/>. It declares no method a client can call.
/// </summary>
public sealed class LiveHub : Hub<ILiveClientProcedures>
{
    readonly ILogger<LiveHub> _logger;

    public LiveHub(ILogger<LiveHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var group = ConnectionDiagnostics.GetConnectionGroup(httpContext);
        var correlationId = ConnectionDiagnostics.GetCorrelationId(httpContext);

        if (string.IsNullOrWhiteSpace(group))
        {
            _logger.LogWarning(
                "Live hub rejected connection {ConnectionId}: the Event query parameter is missing. CorrelationId {CorrelationId}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
                Context.ConnectionId,
                correlationId,
                ConnectionDiagnostics.GetClientName(httpContext),
                ConnectionDiagnostics.GetClientVersion(httpContext),
                ConnectionDiagnostics.GetInstanceId()
            );
            throw new InvalidOperationException(
                "SignalR connection rejected because the event ID query parameter is missing."
            );
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogInformation(
            "Live hub connected {ConnectionId} to Event {EventId}. CorrelationId {CorrelationId}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
            Context.ConnectionId,
            group,
            correlationId,
            ConnectionDiagnostics.GetClientName(httpContext),
            ConnectionDiagnostics.GetClientVersion(httpContext),
            ConnectionDiagnostics.GetInstanceId()
        );
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var httpContext = Context.GetHttpContext();
        var group = ConnectionDiagnostics.GetConnectionGroup(httpContext);
        if (!string.IsNullOrWhiteSpace(group))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        if (exception == null)
        {
            _logger.LogInformation(
                "Live hub disconnected {ConnectionId} from Event {EventId}. CorrelationId {CorrelationId}.",
                Context.ConnectionId,
                group,
                ConnectionDiagnostics.GetCorrelationId(httpContext)
            );
            return;
        }

        _logger.LogWarning(
            exception,
            "Live hub disconnected {ConnectionId} from Event {EventId} with an error. CorrelationId {CorrelationId}.",
            Context.ConnectionId,
            group,
            ConnectionDiagnostics.GetCorrelationId(httpContext)
        );
    }
}
