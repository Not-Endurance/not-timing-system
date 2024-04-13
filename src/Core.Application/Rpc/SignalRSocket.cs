using Core.ConventionalServices;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Core.Application.CoreApplicationConstants;

namespace Core.Application.Rpc;

public class SignalRSocket : IRpcSocket, IAsyncDisposable, ISingletonService
{
    private const int AUTOMATIC_RECONNECT_ATTEMPTS = 3;
    public event EventHandler<RpcConnectionStatus>? ServerConnectionChanged;
    public event EventHandler<string>? ServerConnectionInfo;
    public event EventHandler<RpcError>? Error;
    private System.Timers.Timer? _reconnectionTimer;
    private int _connectionClosedReconnectAttempts;


    public bool IsConnected => this.Connection?.State == HubConnectionState.Connected;

    private readonly RpcContext _context;

    private CancellationTokenSource? reconnectTokenSource;
    private readonly string _name;

    public SignalRSocket()
    {
        _context = new RpcContext(RpcProtocls.Http, RPC_PORT, RPC_ENDPOINT);
        _name = GetType().Name;
    }

    // Necessary because this.Connection instance is not intialized 
    // when procedures are reigstered in the child constructor
    internal List<Action<HubConnection>> Procedures { get; } = new();
    internal HubConnection? Connection { get; private set; }

    public virtual async Task Connect(string host)
    {
        await InternalConnect(host, 0);
    }

    public virtual async Task Disconnect()
    {
        if (this.Connection == null || !this.IsConnected)
        {
            return;
        }
        this.reconnectTokenSource!.Cancel();
        reconnectTokenSource!.Dispose();
        await this.Connection.StopAsync();
        RaiseDisconnected();
    }

    public async ValueTask DisposeAsync()
    {
        if (this.Connection == null)
        {
            return;
        }
        this.Connection.Reconnected -= HandleReconnected;
        this.Connection.Reconnecting -= HandleReconnecting;
        this.Connection.Closed -= HandleClosed;
        await this.Connection.DisposeAsync();
        this.reconnectTokenSource?.Dispose();
        // Might occasionally tigger ObjectDiscpossedException if timer.Elapsed attempts to run
        // during or after Dispose. See https://codereview.stackexchange.com/questions/223877/safe-dispose-of-timer
        // for potential solutions
        _reconnectionTimer?.Dispose();
    }

    private async Task InternalConnect(string host, int reconnectAttempts)
    {
        if (IsConnected)
        {
            ServerConnectionInfo?.Invoke(this, $"{this.GetType().Name} is already connected");
            return;
        }
        if (this.Connection == null)
        {
            this.ConfigureConnection(host);
        }
        try
        {
            this.reconnectTokenSource = new CancellationTokenSource();
            RaiseConnecting();
            await this.Connection!.StartAsync();
            this.RaiseConnected();
        }
        catch (Exception ex)
        {
            if (HasReachedReconnectionAttemptLimit(++reconnectAttempts))
            {
                RaiseDisconnected(ex);
                return;
            }
            await Task.Delay(TimeSpan.FromSeconds(5));
            await InternalConnect(host, reconnectAttempts);
        }
    }

    private void ConfigureConnection(string host)
    {
        _context.Host = host;
        this.Connection = new HubConnectionBuilder()
            .WithUrl(this._context.Url)
            .Build();
        this.Connection.Reconnected += HandleReconnected;
        this.Connection.Reconnecting += HandleReconnecting;
        this.Connection.Closed += HandleClosed;
        foreach (var registerProcedure in Procedures)
        {
            registerProcedure(this.Connection);
        }
    }

    private Task HandleReconnected(string connectionId)
    {
        RaiseConnected($"SignalR automatic reconnected: {connectionId}");
        return Task.CompletedTask;
    }

    private Task HandleReconnecting(Exception exception)
    {
        RaiseReconnecting($"SignalR automatic reconnecting: {exception.Message}");
        return Task.CompletedTask;
    }

    private Task HandleClosed(Exception exception)
    {
        // This check is also necessary here, because if the server hub cannot be constructed (DI error for example)
        // SignalR keeps closing each connection to that hub as soon as it is created
        // Maybe test again with static connection?
        if (HasReachedReconnectionAttemptLimit(++_connectionClosedReconnectAttempts))
        {
            RaiseDisconnected(exception);
        }
        else
        {
            BeginReconnecting(this.reconnectTokenSource!.Token, exception, () => { _connectionClosedReconnectAttempts = 0; });

        }
        return Task.CompletedTask;
    }

    private void BeginReconnecting(CancellationToken cancellationToken, Exception? error, Action onSuccess)
    {
        this.RaiseDisconnected(error);
        RaiseConnecting();
        var reconnectAttempts = 0;
        _reconnectionTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
        _reconnectionTimer.Elapsed += async (s, e) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                ServerConnectionInfo?.Invoke(this, "Reconecting stopped due to cancelation request");
                _reconnectionTimer.Stop();
                _reconnectionTimer.Dispose();
            }
            try
            {
                await this.Connection!.StartAsync();
                if (this.Connection.State == HubConnectionState.Connected)
                {
                    this.RaiseConnected();
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
                    RaiseDisconnected(new Exception("Automatic reconnection reached attempt limits. Try to reconnect manually"));
                    _reconnectionTimer.Stop();
                    _reconnectionTimer.Dispose();
                }
            }
        };
        _reconnectionTimer.Start();
    }

    private bool HasReachedReconnectionAttemptLimit(int attempts)
    {
        return attempts >= AUTOMATIC_RECONNECT_ATTEMPTS;
    }
    internal void RaiseError(Exception exception, string? procedure, params object?[] arguments)
    {
        var message = procedure == null
            ? $"RpcClient error : {exception.Message}"
            : $"RpcClient error in '{procedure}': {exception.Message}";
        Console.WriteLine(message);
        var error = new RpcError(exception, procedure, arguments);
        this.Error?.Invoke(this, error);
    }

    private void RaiseDisconnected(Exception? ex = default)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Disconnected);
        ServerConnectionInfo?.Invoke(_name, ex?.Message ?? "Disconnected manually");
    }

    private void RaiseReconnecting(Exception ex)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{ex.Message}. Attempting to reconnect");
    }
    private void RaiseReconnecting(string message)
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{message}. Attempting to reconnect");
    }
    private void RaiseConnecting()
    {
        ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
    }
    private void RaiseConnected(string? message = null)
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
    bool IsConnected { get; }
    Task Connect(string host);
    Task Disconnect();
}

public enum RpcConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
}
