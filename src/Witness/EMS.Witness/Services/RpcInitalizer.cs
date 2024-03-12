using Core.Application.Rpc;
using Core.Application.Services;
using Core.ConventionalServices;
using EMS.Witness.Platforms.Services;
using EMS.Witness.Shared.Toasts;
using System.Net;
using static Core.Application.CoreApplicationConstants;

namespace EMS.Witness.Services;

public class RpcInitalizer : IRpcInitalizer
{
    private const string ALEX_HOME_WORKSTATION_IP = "localhost";

    private readonly IToaster toaster;
    private readonly IHandshakeService _handshakeService;
    private readonly IEnumerable<IRpcClient> rpcClients;
	private readonly IPermissionsService permissionsService;
	private readonly WitnessContext context;

	public RpcInitalizer(
        IHandshakeService handshakeService,
        IWitnessContext context,
		IEnumerable<IRpcClient> rpcClients,
		IPermissionsService permissionsService,
		IToaster toaster)
    {
		this.context = (WitnessContext)context;
        _handshakeService = handshakeService;
        this.rpcClients = rpcClients;
		this.permissionsService = permissionsService;
		this.toaster = toaster;
    }

    public async Task StartConnections()
	{
		if (!await this.permissionsService.HasNetworkPermissions())
		{
            this.toaster.Add(
                "Network permission rejected",
                "eWitness app cannot operate without Network permissions. Grant permissions in device settings.",
                UiColor.Danger);
			return;
        }
		try
		{
            this.context.RaiseIsHandshakingEvent(true);
#if DEBUG
			var host = ALEX_HOME_WORKSTATION_IP;
#else
			var ip = await _handshakeService.Handshake(Apps.WITNESS, CancellationToken.None);
            if (ip == null)
            {
                throw new Exception("Server broadcast received, but payload does not contain an IP address");
            }
#endif
            this.context.RaiseIsHandshakingEvent(false);

            foreach (var client in this.rpcClients.Where(x => !x.IsConnected))
            {
				await client.Connect(host); 
            }
        }
		catch (Exception exception)
		{
			this.ToastError(exception);
		}
		finally
		{
			this.context.RaiseIsHandshakingEvent(false);
		}
	}

	private void ToastError(Exception exception)
	{
		this.toaster.Add(exception.Message, exception?.StackTrace, UiColor.Danger, 30);
	}
}

public interface IRpcInitalizer : ISingletonService
{
	Task StartConnections();
}
