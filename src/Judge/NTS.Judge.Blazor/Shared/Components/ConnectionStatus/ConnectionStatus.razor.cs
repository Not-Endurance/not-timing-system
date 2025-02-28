using MudBlazor;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;

namespace NTS.Judge.Blazor.Shared.Components.ConnectionStatus;

public partial class ConnectionStatus
{
    [Inject]
    IConnectionsBehind ConnectionsBehind { get; set; } = default!;

    Color SpinnerColor => GetSpinnerColor();

    protected override async Task OnInitializedAsync()
    {
        await Observe(ConnectionsBehind);
    }

    Color GetSpinnerColor()
    {
        if (ConnectionsBehind.ServerConnectionStatus == RpcConnectionStatus.Disconnected)
        {
            return Color.Error;
        }
        else
        {
            return Color.Warning;
        }
    }
}
