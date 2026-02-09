using System.Timers;
using MudBlazor;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Blazor.Components;

namespace NTS.Judge.Blazor.Features.Socket;

public class SocketStatusBehind : NComponent, IDisposable
{
    readonly System.Timers.Timer _timer;

    public SocketStatusBehind()
    {
        _timer = new System.Timers.Timer(TimeSpan.FromSeconds(1));
        _timer.Elapsed += HandleElapsed;
    }

    [Inject]
    ISocketService ConnectionsBehind { get; set; } = default!;

    protected bool IsConnected => ConnectionsBehind.IsConnected;
    protected int ConnectionsCount => ConnectionsBehind.RemoteConnections.Count();

    protected override void OnInitialized()
    {
        _timer.Start();
    }

    protected Color GetSpinnerColor()
    {
        if (ConnectionsBehind.Status == SocketConnectionStatus.Disconnected)
        {
            return Color.Error;
        }
        else
        {
            return Color.Warning;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }

    // ReSharper disable once AsyncVoidMethod
    async void HandleElapsed(object? sender, ElapsedEventArgs e)
    {
        await InvokeRender();
    }
}
