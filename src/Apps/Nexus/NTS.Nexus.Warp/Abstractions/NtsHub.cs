using Microsoft.AspNetCore.SignalR;
using Not.Application.RPC;
using NTS.Nexus.Warp.ConnectionDiagnostics;
using System.Diagnostics;

namespace NTS.Nexus.Warp.Abstractions;

public abstract class NtsHub<T> : Hub<T>
    where T : class
{
    readonly ILogger<NtsHub<T>> _logger;

    public NtsHub(ILogger<NtsHub<T>> logger)
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
        var correlationId = WarpConnectionDiagnostics.GetCorrelationId(httpContext);
        var clientName = WarpConnectionDiagnostics.GetClientName(httpContext);
        var clientVersion = WarpConnectionDiagnostics.GetClientVersion(httpContext);

        _logger.LogInformation(
            "Warp hub {HubName} OnConnectedAsync started. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}, Client {ClientName}, Version {ClientVersion}, InstanceId {InstanceId}.",
            GetType().Name,
            Context.ConnectionId,
            correlationId,
            GetConnectionGroup(),
            clientName,
            clientVersion,
            WarpConnectionDiagnostics.GetInstanceId()
        );

        if (query == null || !query.TryGetValue(RpcConstants.CONNECTION_GROUP_KEY, out var value))
        {
            const string message = "SignalR connection rejected because the event ID query parameter is missing.";
            _logger.LogWarning(
                "Warp hub {HubName} rejected connection {ConnectionId}. CorrelationId {CorrelationId}. Reason: {Message}",
                GetType().Name,
                Context.ConnectionId,
                correlationId,
                message
            );
            throw new InvalidOperationException(message);
        }

        try
        {
            var enduranceEventId = value.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, enduranceEventId);
            stopwatch.Stop();
            _logger.LogInformation(
                "Warp hub {HubName} OnConnectedAsync completed in {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
                GetType().Name,
                stopwatch.ElapsedMilliseconds,
                Context.ConnectionId,
                correlationId,
                enduranceEventId
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Warp hub {HubName} OnConnectedAsync failed after {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
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
        var correlationId = WarpConnectionDiagnostics.GetCorrelationId(httpContext);
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
                    "Warp hub {HubName} OnDisconnectedAsync completed in {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
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
                    "Warp hub {HubName} disconnected with an error after {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
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
                "Warp hub {HubName} OnDisconnectedAsync failed after {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, Group {ConnectionGroup}.",
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
