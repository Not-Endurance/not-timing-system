using Core.Application.Rpc.Procedures;
using EMS.Judge.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc.Hubs;

public class LoggingHub : Hub, ILoggingHubProcedures
{
	private readonly IRpcClientLogger _rpcClientLogger;

	public LoggingHub(IRpcClientLogger rpcClientLogger)
    {
		_rpcClientLogger = rpcClientLogger;
	}

    public async Task Log(string clientId, string message)
	{
		await _rpcClientLogger.Log(clientId, message);
	}

	public async Task LogError(string clientId, Exception exception)
	{
		await _rpcClientLogger.Log(clientId, exception);
	}
}
