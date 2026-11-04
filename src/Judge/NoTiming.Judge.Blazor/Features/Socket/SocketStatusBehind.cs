using System.Timers;
using MudBlazor;
using NC.Application.RPC;
using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Socket;

namespace NoTiming.Judge.Blazor.Features.Socket;

public class SocketStatusBehind : NComponent, IDisposable
{
    readonly System.Timers.Timer _timer;

    public SocketStatusBehind()
    {
        _timer = new System.Timers.Timer(TimeSpan.FromSeconds(1));
        _timer.Elapsed += HandleElapsed;
    }

    [Inject]
    INoTimingSocketService SocketContext { get; set; } = default!;

    protected bool IsConnected => SocketContext.IsConnected;

    protected string? EventName => SocketContext.Event?.Name;

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

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    async void HandleElapsed(object? sender, ElapsedEventArgs e)
    {
        await InvokeRender();
    }
}
