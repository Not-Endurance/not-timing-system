using Core.Application.Rpc;
using Core.Events;
using EMS.Witness.Rpc;
using EMS.Witness.Services;
using EMS.Witness.Shared.Toasts;

namespace EMS.Witness;

public partial class App : Application
{
    private readonly IPersistenceService persistence;
    private readonly IStartlistClient startlistClient;
    private readonly IStartlistService startlistService;
    private readonly IToaster toaster;
    private readonly IRpcInitalizer rpcService;
    private readonly IParticipantsClient participantsClient;
    private readonly IParticipantsService participantsService;
	private bool isDeactivated;

    public App(
		IPersistenceService persistence,
		IStartlistClient startlistClient,
		IStartlistService startlistService,
		IToaster toaster,
		IEnumerable<IRpcClient> rpcClients,
		IRpcInitalizer rpcService,
		IParticipantsClient arrivelistClient,
		IParticipantsService arrivelistService)
	{
		this.InitializeComponent();
        this.MainPage = new MainPage();
        this.persistence = persistence;
        this.startlistClient = startlistClient;
        this.startlistService = startlistService;
        this.toaster = toaster;
        this.rpcService = rpcService;
        this.participantsClient = arrivelistClient;
        this.participantsService = arrivelistService;
        this.HandleRpcErrors(rpcClients);
		this.HandleCoreErrors();
		this.AttachEventHandlers();
    }

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);

		window.Resumed += async (s, e) => await SafeAction(async () =>
		{
			this.isDeactivated = false;
			await this.rpcService.StartConnections();
		});
		window.Deactivated += async (s, e) => await SafeAction(async () =>
		{
			this.isDeactivated = true;
			await this.persistence.Store();
		});

		return window;
	}

	private void AttachEventHandlers()
	{

        this.participantsClient.Updated += (sender, args) => this.participantsService.Update(args.entry, args.action);
        this.participantsClient.ServerConnectionChanged += async (sender, status) => 
		{
			if (status == RpcConnectionStatus.Connected)
			{
				await this.participantsService.Load();
			}
        };

		this.startlistClient.Updated += (s, a) => this.startlistService.Update(a.entry, a.action);
        this.startlistClient.ServerConnectionChanged += async (sender, status) =>
        {
            if (status == RpcConnectionStatus.Connected)
            {
                await this.startlistService.Load();
            }
        };
    }

	private async Task SafeAction(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			await Application.Current!.MainPage!.DisplayAlert(ex.Message, ex.StackTrace, "damn");
        }
	}

	private void HandleRpcErrors(IEnumerable<IRpcClient> rpcClients)
	{
		foreach (var client in rpcClients)
		{
            client.Error += (sender, error) =>
            {
                this.toaster.Add("RPC client error", $"{error.Procedure}: {error.Exception.Message}", UiColor.Danger, 30);
            };
        }
	}

	private void HandleCoreErrors()
	{
		CoreEvents.ErrorEvent += (sender, error) =>
		{
			this.toaster.Add(error.Message, error.StackTrace, UiColor.Danger, 30);
		};
	}
}
