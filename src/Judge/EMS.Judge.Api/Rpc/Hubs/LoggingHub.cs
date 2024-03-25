using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using EMS.Judge.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc.Hubs;

public class LoggingHub : Hub, ILoggingHubProcedures
{
	private readonly IRpcClientLogger _rpcClientLogger;

	public LoggingHub(IRpcClientLogger rpcClientLogger)
    {
		_rpcClientLogger = rpcClientLogger;
	}

	public Task ReceiveLog(RpcLog log)
	{
		_rpcClientLogger.Log(log);
		return Task.CompletedTask;
	}

	// =====================================================================
	// == POC by using a Hub to invoke Client procedures (requires .net8) ==
	// =====================================================================
	//public override Task OnConnectedAsync()
	//{
	//	_service.AddConnection(Context.ConnectionId);
	//	return base.OnConnectedAsync();
	//}

	//public override Task OnDisconnectedAsync(Exception exception)
	//{
	//	_service.RemoveConnection(Context.ConnectionId);
	//	// TODO: logging here?
	//	return base.OnDisconnectedAsync(exception);
	//}

	//public class Service : BackgroundService
	//{
	//	private readonly IHubContext<LoggingHub, ILoggingHubProcedures> _hubContext;
	//	private readonly IRpcClientLogger _rpcClientLogger;
	//	private readonly List<string> _connections = new();

	//	public Service(IHubContext<LoggingHub, ILoggingHubProcedures> hubContext, IRpcClientLogger rpcClientLogger)
	//       {
	//		_hubContext = hubContext;
	//		_rpcClientLogger = rpcClientLogger;
	//	}

	//       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	//	{
	//		var interval = TimeSpan.FromSeconds(5);
	//		while (!stoppingToken.IsCancellationRequested)
	//		{
	//			var clientLogs = await GetClientLogs();
	//			_rpcClientLogger.Log(clientLogs);

	//			await Task.Delay(interval, stoppingToken);
	//		}
	//	}

	//	internal void AddConnection(string connectionId)
	//	{
	//		_connections.Add(connectionId);
	//	}

	//	internal void RemoveConnection(string connectionId) 
	//	{
	//		_connections.Remove(connectionId);
	//	}

	//	private async Task<IEnumerable<RpcLog>> GetClientLogs()
	//	{
	//		var result = new List<RpcLog>();
	//		foreach (var connectionId in _connections)
	//		{
	//			var logs = await _hubContext.Clients.Client(connectionId).ReceiveLogs();
	//			result.AddRange(logs);
	//		}
	//		return result;
	//	}
	//}
}
