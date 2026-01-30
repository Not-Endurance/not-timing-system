using System.Timers;
using MudBlazor;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Blazor.Components;
using Timer = System.Timers.Timer;

namespace NTS.Judge.Blazor.Shared.Components.ConnectionStatus;

public partial class ConnectionStatus : NComponent, IDisposable
{
    readonly Timer _timer;

    public ConnectionStatus()
    {
        _timer = new Timer(TimeSpan.FromSeconds(1));
        _timer.Elapsed += HandleElapsed;
    }

    [Inject]
    IConnectionsBehind ConnectionsBehind { get; set; } = default!;

    Color SpinnerColor => GetSpinnerColor();

    protected override void OnInitialized()
    {
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
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

    // ReSharper disable once AsyncVoidMethod
    async void HandleElapsed(object? sender, ElapsedEventArgs e)
    {
        await InvokeRender();
    }
}
