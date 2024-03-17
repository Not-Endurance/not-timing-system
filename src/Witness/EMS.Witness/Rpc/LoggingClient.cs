using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;

namespace EMS.Witness.Rpc;

public class LoggingClient : RpcClient, IWitnessLogger
{
	public LoggingClient(RpcContext context) : base(context)
	{
	}

	public void Log(string message)
	{
		var clientId = GetClientId();
		var log = new RpcLog(clientId, message);
		SendInBackground(log);
	}

	public void Log(Exception exception)
	{
		var clientId = GetClientId();
		var log = new RpcLog(clientId, exception);
		SendInBackground(log);
	}

	private string GetClientId() => $"{DeviceInfo.Current.Model}-{DeviceInfo.Current.Version}";

	private void SendInBackground(RpcLog log)
	{
		_ = Task.Run(() => EnsureSentLog(log));
	}

	private async Task EnsureSentLog(RpcLog log)
	{
		var result = await Send(log);
		if (!result.IsSuccessful)
		{
			await Task.Delay(TimeSpan.FromSeconds(5));
			await EnsureSentLog(log);
		}
	}
	private async Task<RpcInvokeResult> Send(RpcLog log)
	{
		return await InvokeHubProcedure(nameof(ILoggingHubProcedures.ReceiveLog), log);
	}
}

public interface IWitnessLogger
{
	void Log(string message);
	void Log(Exception exception);
}