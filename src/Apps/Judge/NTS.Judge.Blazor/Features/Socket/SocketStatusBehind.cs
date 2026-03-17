using System.Timers;
using MudBlazor;
using Not.Application.RPC.SignalR;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Socket;

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
    INtsSocketService SocketContext { get; set; } = default!;

    protected bool IsConnected => SocketContext.IsConnected;

    protected string? EventName => SocketContext.Event?.PopulatedPlace.City;

    protected override void OnInitialized()
    {
        try
        {
            _timer.Start();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected Color GetSpinnerColorSafe()
    {
        if (SocketContext.Status == SocketConnectionStatus.Disconnected)
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

    async void HandleElapsed(object? sender, ElapsedEventArgs e)
    {
        await InvokeRender();
    }
}
