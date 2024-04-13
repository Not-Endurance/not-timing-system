using Core.Application.Rpc;

namespace EMS.Witness.Rpc;

public class LoggingClient : IWitnessLogger
{
	public LoggingClient()
	{
	}

	public void Log(string message)
	{
		var clientId = GetClientId();
		var log = new RpcLog(clientId, message);
		// TODO: Send http log
	}

	public void Log(Exception exception)
	{
		var clientId = GetClientId();
		var log = new RpcLog(clientId, exception);
        // TODO: Send http log
    }

    private string GetClientId() => $"{DeviceInfo.Current.Manufacturer}-{DeviceInfo.Current.Name}-{DeviceInfo.Current.Version}";
}

public interface IWitnessLogger
{
	void Log(string message);
	void Log(Exception exception);
}