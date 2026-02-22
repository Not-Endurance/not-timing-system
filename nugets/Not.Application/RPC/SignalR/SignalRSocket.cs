using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Not.Logging;
using Not.Notify;
using Not.Serialization.JSON;
using Not.Strings;

namespace Not.Application.RPC.SignalR;

public class SignalRSocket : IRpcSocket, IAsyncDisposable
{
    readonly RpcSettings _context;
    readonly string _name;
    readonly INotifier _notifier;

    public SignalRSocket(IOptions<RpcSettings> options, INotifier notifier)
    {
        _context = Validate(options.Value);
        _name = GetType().Name;
        _notifier = notifier;
    }

    // Necessary because this.Connection instance is not intialized
    // when procedures are reigstered in the child constructor
    internal List<Action<HubConnection>> Procedures { get; } = [];

    public event EventHandler<SocketConnectionStatus>? ServerConnectionChanged;
    public event EventHandler<string>? ServerConnectionInfo;
    public event EventHandler<RpcError>? Error;

    public HubConnection? Connection { get; private set; }

    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

    public void RaiseError(Exception exception, string? procedure, params object?[] arguments)
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

    public virtual async Task Connect(string groupId)
    {
        await InternalConnect(groupId);
    }

    public virtual async Task Connect()
    {
        await InternalConnect(null);
    }

    public virtual async Task Disconnect()
    {
        if (Connection == null || !IsConnected)
        {
            return;
        }
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
    }

    async Task InternalConnect(string? groupId)
    {
        if (IsConnected)
        {
            var message = $"{GetType().Name} is already connected";
            ServerConnectionInfo?.Invoke(this, message);
            return;
        }
        try
        {
            ConfigureConnection(groupId);
            RaiseConnecting();
            if (!IsConnected)
            {
                await Connection!.StartAsync();
            }
            RaiseConnected();
        }
        catch (Exception ex)
        {
            _notifier.Error(ex);
        }
    }

    void ConfigureConnection(string? groupId)
    {
        var url = _context.Url;
        if (groupId != null)
        {
            var query = new Dictionary<string, string?>
            {
                { RpcConstants.CONNECTION_GROUP_KEY, groupId }
            };
            url = QueryHelpers.AddQueryString(url, query);
        }

        Connection = new HubConnectionBuilder()
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = new NJsonSettings())
            .WithUrl(url)
            .WithAutomaticReconnect(new AutomaticReconnectSetting())
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
        RaiseReconnecting($"SignalR automatic reconnecting: {exception?.Message ?? "something went wrong"}");
        return Task.CompletedTask;
    }

    Task HandleClosed(Exception? exception)
    {
        // This check is also necessary here, because if the server hub cannot be constructed (DI error for example)
        // SignalR keeps closing each connection to that hub as soon as it is created
        // Maybe test again with static connection?
        RaiseDisconnected(exception);
        return Task.CompletedTask;
    }

    void RaiseDisconnected(Exception? _ = default)
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Disconnected);
    }

    void RaiseReconnecting(string message)
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{message}. Attempting to reconnect");
    }

    void RaiseConnecting()
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Connecting);
    }

    void RaiseConnected(string? message = null)
    {
        ServerConnectionChanged?.Invoke(_name, SocketConnectionStatus.Connected);
        if (message != null)
        {
            ServerConnectionInfo?.Invoke(_name, message);
        }
    }

    static RpcSettings Validate(RpcSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.HubPattern))
        {
            throw new Exception(
                $"Invalid SignalR configuration - Host: '{settings.Host}', Pattern: '{settings.HubPattern}'"
            );
        }
        return settings;
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
    HubConnection? Connection { get; }
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
