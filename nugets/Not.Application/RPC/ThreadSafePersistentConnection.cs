using System.Threading;

namespace Not.Application.RPC;

public abstract class ThreadSafePersistentConnection<TConnectionContext> : IAsyncDisposable
    where TConnectionContext : class
{
    readonly string _name;
    readonly SemaphoreSlim _connectionGate = new(1, 1);
    CancellationTokenSource? _activeConnectCancellation;
    Task? _activeConnectTask;

    protected ThreadSafePersistentConnection(string? name = null)
    {
        _name = name ?? GetType().Name;
    }

    protected abstract bool IsConnectionActive { get; }
    protected abstract Task ConfigureConnectionAsync(
        TConnectionContext connectionContext,
        CancellationToken cancellationToken
    );
    protected abstract Task StartConnectionCoreAsync(
        TConnectionContext connectionContext,
        CancellationToken cancellationToken
    );
    protected abstract Task CleanupConnectionAsync(string reason);

    protected TConnectionContext? CurrentConnectionContext { get; private set; }

    public event EventHandler<SocketConnectionStatus>? ServerConnectionChanged;
    public event EventHandler<string>? ServerConnectionInfo;

    protected async Task ConnectAsync(TConnectionContext connectionContext, CancellationToken operationCancellationToken)
    {
        Task connectTask;

        await _connectionGate.WaitAsync(operationCancellationToken);
        try
        {
            if (_activeConnectTask is { IsCompleted: false } activeConnectTask)
            {
                connectTask = activeConnectTask;
            }
            else
            {
                _activeConnectCancellation?.Dispose();
                _activeConnectCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationCancellationToken);
                connectTask = InternalConnect(connectionContext, _activeConnectCancellation.Token);
                _activeConnectTask = connectTask;
            }
        }
        finally
        {
            _connectionGate.Release();
        }

        try
        {
            await connectTask;
        }
        finally
        {
            await _connectionGate.WaitAsync(CancellationToken.None);
            try
            {
                if (ReferenceEquals(_activeConnectTask, connectTask))
                {
                    _activeConnectTask = null;
                    _activeConnectCancellation?.Dispose();
                    _activeConnectCancellation = null;
                }
            }
            finally
            {
                _connectionGate.Release();
            }
        }
    }

    protected async Task DisconnectAsync(string reason, bool clearContext = true)
    {
        Task? activeConnectTask;

        await _connectionGate.WaitAsync();
        try
        {
            _activeConnectCancellation?.Cancel();
            activeConnectTask = _activeConnectTask;
        }
        finally
        {
            _connectionGate.Release();
        }

        if (activeConnectTask != null && !activeConnectTask.IsCompleted)
        {
            try
            {
                await activeConnectTask;
            }
            catch (Exception ex)
            {
                await OnActiveConnectInterruptedAsync(ex, reason);
            }
        }

        await _connectionGate.WaitAsync();
        try
        {
            if (clearContext)
            {
                CurrentConnectionContext = null;
            }

            await CleanupConnectionAsync(reason);
            RaiseDisconnected();
            RaiseInfo(GetDisconnectedMessage(reason));
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    protected void RaiseDisconnected()
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Disconnected);
    }

    protected void RaiseReconnecting(string message)
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{message}. Attempting to reconnect");
    }

    protected void RaiseConnecting()
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Connecting);
    }

    protected void RaiseConnected(string? message = null)
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Connected);
        if (message != null)
        {
            ServerConnectionInfo?.Invoke(_name, message);
        }
    }

    protected void RaiseInfo(string message)
    {
        ServerConnectionInfo?.Invoke(_name, message);
    }

    protected virtual string GetAlreadyConnectedMessage(TConnectionContext connectionContext)
    {
        return $"{GetType().Name} is already connected";
    }

    protected virtual string GetConnectStartedMessage(TConnectionContext connectionContext)
    {
        return $"{GetType().Name} connect attempt started.";
    }

    protected virtual string? GetConnectedMessage(TConnectionContext connectionContext)
    {
        return null;
    }

    protected virtual string GetTimedOutMessage(TConnectionContext connectionContext)
    {
        return $"{GetType().Name} connect attempt timed out.";
    }

    protected virtual string GetCanceledMessage(TConnectionContext connectionContext)
    {
        return $"{GetType().Name} connect attempt canceled.";
    }

    protected virtual string GetFailedMessage(TConnectionContext connectionContext)
    {
        return $"{GetType().Name} connect attempt failed.";
    }

    protected virtual string GetDisconnectedMessage(string reason)
    {
        return $"{GetType().Name} disconnected.";
    }

    protected virtual string GetFailedCleanupReason()
    {
        return "failed connect cleanup";
    }

    protected virtual Task OnConnectStartingAsync(TConnectionContext connectionContext)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnConnectedAsync(TConnectionContext connectionContext)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnTimedOutAsync(TConnectionContext connectionContext, OperationCanceledException exception)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnCanceledAsync(TConnectionContext connectionContext, OperationCanceledException exception)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnFailedAsync(TConnectionContext connectionContext, Exception exception)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnConnectFinishedAsync(TConnectionContext connectionContext)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnActiveConnectInterruptedAsync(Exception exception, string reason)
    {
        return Task.CompletedTask;
    }

    protected virtual TimeSpan? GetConnectTimeout(TConnectionContext connectionContext)
    {
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        Task? activeConnectTask;

        await _connectionGate.WaitAsync();
        try
        {
            _activeConnectCancellation?.Cancel();
            activeConnectTask = _activeConnectTask;
        }
        finally
        {
            _connectionGate.Release();
        }

        if (activeConnectTask != null && !activeConnectTask.IsCompleted)
        {
            try
            {
                await activeConnectTask;
            }
            catch (Exception ex)
            {
                await OnActiveConnectInterruptedAsync(ex, "dispose");
            }
        }

        await _connectionGate.WaitAsync();
        try
        {
            await CleanupConnectionAsync("dispose");
        }
        finally
        {
            _connectionGate.Release();
            _connectionGate.Dispose();
        }
    }

    async Task InternalConnect(TConnectionContext connectionContext, CancellationToken cancellationToken)
    {
        if (IsConnectionActive)
        {
            RaiseInfo(GetAlreadyConnectedMessage(connectionContext));
            return;
        }

        CurrentConnectionContext = connectionContext;
        using var timeoutCancellation = CreateTimeoutCancellation(GetConnectTimeout(connectionContext));
        using var linkedCancellation = timeoutCancellation == null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        var effectiveCancellationToken = linkedCancellation?.Token ?? cancellationToken;

        try
        {
            await _connectionGate.WaitAsync(effectiveCancellationToken);
            try
            {
                await ConfigureConnectionAsync(connectionContext, effectiveCancellationToken);
            }
            finally
            {
                _connectionGate.Release();
            }

            RaiseConnecting();
            RaiseInfo(GetConnectStartedMessage(connectionContext));
            await OnConnectStartingAsync(connectionContext);

            await StartConnectionCoreAsync(connectionContext, effectiveCancellationToken);

            RaiseConnected(GetConnectedMessage(connectionContext));
            await OnConnectedAsync(connectionContext);
        }
        catch (OperationCanceledException ex) when (timeoutCancellation?.IsCancellationRequested == true)
        {
            await CleanupFailedConnectionAsync();
            RaiseDisconnected();
            RaiseInfo(GetTimedOutMessage(connectionContext));
            await OnTimedOutAsync(connectionContext, ex);
        }
        catch (OperationCanceledException ex)
        {
            await CleanupFailedConnectionAsync();
            RaiseDisconnected();
            RaiseInfo(GetCanceledMessage(connectionContext));
            await OnCanceledAsync(connectionContext, ex);
        }
        catch (Exception ex)
        {
            await CleanupFailedConnectionAsync();
            RaiseDisconnected();
            RaiseInfo(GetFailedMessage(connectionContext));
            await OnFailedAsync(connectionContext, ex);
        }
        finally
        {
            await OnConnectFinishedAsync(connectionContext);
        }
    }

    async Task CleanupFailedConnectionAsync()
    {
        await _connectionGate.WaitAsync();
        try
        {
            await CleanupConnectionAsync(GetFailedCleanupReason());
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    static CancellationTokenSource? CreateTimeoutCancellation(TimeSpan? timeout)
    {
        return timeout == null ? null : new CancellationTokenSource(timeout.Value);
    }
}

public interface IRpcSocket
{
    /// <summary>
    /// 'true' means connected; 'false' - disconnected;
    /// </summary>
    event EventHandler<SocketConnectionStatus>? ServerConnectionChanged;
    event EventHandler<string>? ServerConnectionInfo;
    event EventHandler<RpcError>? Error;
    Microsoft.AspNetCore.SignalR.Client.HubConnection? Connection { get; }
    bool IsConnected { get; }
    Task Connect();
    Task Connect(string groupId);
    Task Disconnect();
    void RaiseError(Exception exception, string? procedure, params object?[] arguments);
}

public enum SocketConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
}
