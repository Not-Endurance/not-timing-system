using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Not.Logging;
using Not.Serialization;

namespace Not.Application.RPC.SignalR;

public class SignalRSocket : IRpcSocket, IAsyncDisposable
{
    const int AUTOMATIC_RECONNECT_ATTEMPTS = 3;

    readonly SignalRContext _context;
    readonly string _name;
    System.Timers.Timer? _reconnectionTimer;
    int _connectionClosedReconnectAttempts;
    CancellationTokenSource? _reconnectTokenSource;

    internal SignalRSocket(SignalRContext? context = null)
    {
        _context =
            context
            ?? throw new ApplicationException(
                $"SignalR socket is not configured. Use '{nameof(RpcServiceCollectionExtensions)}' to configure the socket"
            );
        _name = GetType().Name;
    }

    // Necessary because this.Connection instance is not intialized
    // when procedures are reigstered in the child constructor
    internal List<Action<HubConnection>> Procedures { get; } = [];

    public event EventHandler<RpcConnectionStatus>? ServerConnectionChanged;
    public event EventHandler<string>? ServerConnectionInfo;
    public event EventHandler<RpcError>? Error;

    public HubConnection? Connection { get; private set; }

    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

    internal void RaiseError(Exception exception, string? procedure, params object?[] arguments)
    {
        var message =
            procedure == null
                ? $"RpcClient error : {exception.Message}"
                : $"RpcClient error in '{procedure}': {exception.Message}";
        Console.WriteLine(message);
        LoggingHelper.Error(message);
        var error = new RpcError(exception, procedure, arguments);
        Error?.Invoke(this, error);
    }

    public virtual async Task Connect()
    {
        await InternalConnect(0);
    }

    public virtual async Task Disconnect()
    {
        if (Connection == null || !IsConnected)
        {
            return;
        }
        _reconnectTokenSource!.Cancel();
        await Connection.StopAsync();
        RaiseDisconnected();
    }

    public async ValueTask DisposeAsync()
    {
        if (Connection == null)
        {
            return;
        }
        Connection.Reconnected -= HandleReconnected;
        Connection.Reconnecting -= HandleReconnecting;
        Connection.Closed -= HandleClosed;
        await Connection.DisposeAsync();
        _reconnectTokenSource?.Dispose();
        // Might occasionally tigger ObjectDiscpossedException if timer.Elapsed attempts to run
        // during or after Dispose. See https://codereview.stackexchange.com/questions/223877/safe-dispose-of-timer
        // for potential solutions
        _reconnectionTimer?.Dispose();
    }

    async Task InternalConnect(int reconnectAttempts)
    {
        if (IsConnected)
        {
            var message = $"{GetType().Name} is already connected";
            ServerConnectionInfo?.Invoke(this, message);
            return;
        }
        if (Connection == null)
        {
            ConfigureConnection();
        }
        try
        {
            _reconnectTokenSource = new CancellationTokenSource();
            RaiseConnecting();
            await Connection!.StartAsync();
            RaiseConnected();
        }
        catch (Exception ex)
        {
            if (HasReachedReconnectionAttemptLimit(++reconnectAttempts))
            {
                RaiseDisconnected(ex);
                return;
            }
            var delay = TimeSpan.FromSeconds(5);
            await Task.Delay(delay);
            await InternalConnect(reconnectAttempts);
        }
    }

    void ConfigureConnection()
    {
        Connection = new HubConnectionBuilder()
            .AddNewtonsoftJsonProtocol(x =>
                x.PayloadSerializerSettings = SerializationExtensions.SETTINGS
            )
            .WithUrl(_context.Url)
            .Build();
        Connection.Reconnected += HandleReconnected;
        Connection.Reconnecting += HandleReconnecting;
        Connection.Closed += HandleClosed;
        foreach (var registerProcedure in Procedures)
        {
            registerProcedure(Connection);
        }
    }

    Task HandleReconnected(string? connectionId)
    {
        RaiseConnected($"SignalR automatic reconnected: {connectionId}");
        return Task.CompletedTask;
    }

    Task HandleReconnecting(Exception? exception)
    {
        RaiseReconnecting(
            $"SignalR automatic reconnecting: {exception?.Message ?? "something went wrong"}"
        );
        return Task.CompletedTask;
    }

    Task HandleClosed(Exception? exception)
    {
        if (_reconnectTokenSource?.IsCancellationRequested ?? true)
        {
            return Task.CompletedTask;
        }
        // This check is also necessary here, because if the server hub cannot be constructed (DI error for example)
        // SignalR keeps closing each connection to that hub as soon as it is created
        // Maybe test again with static connection?
        if (HasReachedReconnectionAttemptLimit(++_connectionClosedReconnectAttempts))
        {
            RaiseDisconnected(exception);
        }
        else
        {
            BeginReconnecting(
                _reconnectTokenSource!.Token,
                exception,
                () => _connectionClosedReconnectAttempts = 0
            );
        }
        return Task.CompletedTask;
    }

    void BeginReconnecting(CancellationToken cancellationToken, Exception? error, Action onSuccess)
    {
        RaiseDisconnected(error);
        RaiseConnecting();
        var reconnectAttempts = 0;
        _reconnectionTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
        _reconnectionTimer.Elapsed += async (s, e) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                ServerConnectionInfo?.Invoke(
                    this,
                    "Reconnecting stopped due to cancelation request"
                );
                _reconnectionTimer.Stop();
                _reconnectionTimer.Dispose();
            }
            try
            {
                await Connection!.StartAsync();
                if (Connection.State == HubConnectionState.Connected)
                {
                    RaiseConnected();
                    onSuccess();
                    _reconnectionTimer.Stop();
                    _reconnectionTimer.Dispose();
                }
            }
            catch (Exception ex)
            {
                RaiseReconnecting(ex);
            }
            finally
            {
                if (HasReachedReconnectionAttemptLimit(++reconnectAttempts))
                {
                    RaiseDisconnected(
                        new Exception(
                            "Automatic reconnection reached attempt limits. Try to reconnect manually"
                        )
                    );
                    _reconnectionTimer.Stop();
                    _reconnectionTimer.Dispose();
                }
            }
        };
        _reconnectionTimer.Start();
    }

    bool HasReachedReconnectionAttemptLimit(int attempts)
    {
        return attempts >= AUTOMATIC_RECONNECT_ATTEMPTS;
    }

    void RaiseDisconnected(Exception? _ = default)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Disconnected);
    }

    void RaiseReconnecting(Exception ex)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{ex.Message}. Attempting to reconnect");
    }

    void RaiseReconnecting(string message)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{message}. Attempting to reconnect");
    }

    void RaiseConnecting()
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
    }

    void RaiseConnected(string? message = null)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connected);
        if (message != null)
        {
            ServerConnectionInfo?.Invoke(_name, message);
        }
    }
}

public interface IRpcSocket
{
    /// <summary>
    /// 'true' means connected; 'false' - disconnected;
    /// </summary>
    event EventHandler<RpcConnectionStatus>? ServerConnectionChanged;
    event EventHandler<string>? ServerConnectionInfo;
    event EventHandler<RpcError>? Error;
    HubConnection? Connection { get; }
    bool IsConnected { get; }
    Task Connect();
    Task Disconnect();
}

public enum RpcConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
}
