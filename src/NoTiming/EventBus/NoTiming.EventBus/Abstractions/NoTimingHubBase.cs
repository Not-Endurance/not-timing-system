using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using NC.Application.RPC;
using NoTiming.EventBus.ConnectionDiagnostics;

namespace NoTiming.EventBus.Abstractions;

public abstract class NoTimingHubBase<T> : Hub<T>
    where T : class
{
    readonly ILogger<NoTimingHubBase<T>> _logger;

    public NoTimingHubBase(ILogger<NoTimingHubBase<T>> logger)
    {
        _logger = logger;
    }

    protected string? GetConnectionGroup()
    {
        return Context.GetHttpContext()!.Request.Query[RpcConstants.CONNECTION_GROUP_KEY].ToString();
    }

    public override async Task OnConnectedAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var httpContext = Context.GetHttpContext();
        var query = httpContext?.Request.Query;
        var correlationId = NoTimingConnectionDiagnostics.GetCorrelationId(httpContext);
        var clientName = NoTimingConnectionDiagnostics.GetClientName(httpContext);
        var clientVersion = NoTimingConnectionDiagnostics.GetClientVersion(httpContext);

        _logger.LogInformation(
            "NoTiming hub {HubName} OnConnectedAsync started. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
            GetType().Name,
            Context.ConnectionId,
            correlationId,
            GetConnectionGroup(),
            clientName,
            clientVersion,
            NoTimingConnectionDiagnostics.GetInstanceId()
        );

        if (query == null || !query.TryGetValue(RpcConstants.CONNECTION_GROUP_KEY, out var value))
        {
            const string message = "SignalR connection rejected because the event ID query parameter is missing.";
            _logger.LogWarning(
                "NoTiming hub {HubName} rejected connection {ConnectionId}. CorrelationId {CorrelationId}. Reason: {Message}",
                GetType().Name,
                Context.ConnectionId,
                correlationId,
                message
            );
            throw new InvalidOperationException(message);
        }

        try
        {
            var eventId = value.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, eventId);
            stopwatch.Stop();
            _logger.LogInformation(
                "NoTiming hub {HubName} OnConnectedAsync completed in {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
                GetType().Name,
                stopwatch.ElapsedMilliseconds,
                Context.ConnectionId,
                correlationId,
                eventId
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "NoTiming hub {HubName} OnConnectedAsync failed after {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
                GetType().Name,
                stopwatch.ElapsedMilliseconds,
                Context.ConnectionId,
                correlationId,
                GetConnectionGroup()
            );
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var stopwatch = Stopwatch.StartNew();
        var httpContext = Context.GetHttpContext();
        var correlationId = NoTimingConnectionDiagnostics.GetCorrelationId(httpContext);
        var connectionGroup = GetConnectionGroup();

        try
        {
            if (!string.IsNullOrWhiteSpace(connectionGroup))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, connectionGroup);
            }

            stopwatch.Stop();
            if (exception == null)
            {
                _logger.LogInformation(
                    "NoTiming hub {HubName} OnDisconnectedAsync completed in {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
                    GetType().Name,
                    stopwatch.ElapsedMilliseconds,
                    Context.ConnectionId,
                    correlationId,
                    connectionGroup
                );
            }
            else
            {
                _logger.LogWarning(
                    exception,
                    "NoTiming hub {HubName} disconnected with an error after {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
                    GetType().Name,
                    stopwatch.ElapsedMilliseconds,
                    Context.ConnectionId,
                    correlationId,
                    connectionGroup
                );
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "NoTiming hub {HubName} OnDisconnectedAsync failed after {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
                GetType().Name,
                stopwatch.ElapsedMilliseconds,
                Context.ConnectionId,
                correlationId,
                connectionGroup
            );
            throw;
        }
    }
}
