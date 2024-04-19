using Core.Application.Rpc;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Enums;
using EMS.Witness.Rpc;
using EMS.Witness.Services;

namespace EMS.Witness;

public partial class App : Application, IDisposable
{
    private readonly IRpcSocket _rpcSocket;
    private readonly IPersistenceService _persistence;
    private readonly IStartlistClient startlistClient;
    private readonly IStartlistService startlistService;
    private readonly IParticipantsClient participantsClient;
    private readonly IParticipantsService participantsService;

    public App(
		IRpcSocket rpcSocket,
		IPersistenceService persistence,
		IStartlistClient startlistClient,
		IStartlistService startlistService,
		IParticipantsClient arrivelistClient,
		IParticipantsService arrivelistService)
	{
		this.InitializeComponent();
        this.MainPage = new MainPage();
        _rpcSocket = rpcSocket;
        _persistence = persistence;
        this.startlistClient = startlistClient;
        this.startlistService = startlistService;
        this.participantsClient = arrivelistClient;
        this.participantsService = arrivelistService;
    }

	protected override Window CreateWindow(IActivationState? activationState) 
	{
		var window = base.CreateWindow(activationState);

		this.AttachEventHandlers();

		window.Deactivated += OnDeactivated;
		window.Destroying += DetachEventHandlers;

		return window;
	}

	public void Dispose()
	{
		throw new NotImplementedException();
	}

    private async void OnDeactivated(object? sender, EventArgs args)
	{
		await Task.WhenAll(
			_persistence.Store(),
			_rpcSocket.Disconnect());
	}

	private void AttachEventHandlers()
	{
		//TODO: Add Initialize method to StartlistService ParticipantService to attach necessary events
		this.participantsClient.Updated += HandleParticipantsUpdate;
		this.startlistClient.Updated += HandleStartlistUpdate;
        _rpcSocket.ServerConnectionChanged += HandleSocketConnectionChanged;
    }

    private void DetachEventHandlers(object? sender, EventArgs args)
	{
		this.participantsClient.Updated -= HandleParticipantsUpdate;
		this.startlistClient.Updated -= HandleStartlistUpdate;
        _rpcSocket.ServerConnectionChanged -= HandleSocketConnectionChanged;
    }

	private void HandleParticipantsUpdate(object? sender, (ParticipantEntry Participant, CollectionAction Action) args)
	{
		this.participantsService.Update(args.Participant, args.Action);
	}

	private void HandleStartlistUpdate(object? sender, (StartlistEntry StartlistEntry, CollectionAction Action) args)
	{
		this.startlistService.Update(args.StartlistEntry, args.Action);
	}

	private async void HandleSocketConnectionChanged(object? sender, RpcConnectionStatus status)
	{
		if (status == RpcConnectionStatus.Connected)
		{
			await this.startlistService.Load();
			await this.participantsService.Load();
        }
	}
}
