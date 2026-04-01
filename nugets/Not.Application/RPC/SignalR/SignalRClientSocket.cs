using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Notify;
using Not.Serialization.JSON;

namespace Not.Application.RPC.SignalR;

public class SignalRSocket : ThreadSafePersistentConnection<SignalRSocket.ConnectionAttemptContext>, IRpcSocket
{
    readonly RpcSettings _context;
    readonly INotifier _notifier;
    readonly IRpcAccessTokenProvider? _accessTokenProvider;
    readonly ILogger<SignalRSocket> _logger;
    readonly ILoggerFactory _loggerFactory;
    string? _lastSuccessfulGroupId;
    ConnectionAttemptContext? _currentAttempt;
    CancellationToken _currentConnectCancellationToken = CancellationToken.None;

    public SignalRSocket(
        IOptions<RpcSettings> options,
        INotifier notifier,
        IRpcAccessTokenProvider? accessTokenProvider = null,
        ILogger<SignalRSocket>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
        : base(nameof(SignalRSocket))
    {
        _context = Validate(options.Value);
        _notifier = notifier;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger ?? NullLogger<SignalRSocket>.Instance;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    // Necessary because this.Connection instance is not intialized
    // when procedures are reigstered in the child constructor
    internal List<Action<HubConnection>> Procedures { get; } = [];
    protected override bool IsConnectionActive => IsHubConnectionActive(Connection);

    public event EventHandler<RpcError>? Error;

    public HubConnection? Connection { get; protected set; }

    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

    protected virtual HubConnection CreateConnection(string url)
    {
        var builder = new HubConnectionBuilder();
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddProvider(new ExistingLoggerFactoryProvider(_loggerFactory));
        });

        return builder
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = new NJsonSettings())
            .WithUrl(url, options => options.AccessTokenProvider = ResolveAccessToken)
            .WithAutomaticReconnect(new AutomaticReconnectSetting())
            .Build();
    }

    protected virtual Task StartConnectionAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    protected virtual Task DisposeConnectionAsync(HubConnection connection)
    {
        return connection.DisposeAsync().AsTask();
    }

    protected override async Task ConfigureConnectionAsync(
        ConnectionAttemptContext attempt,
        CancellationToken cancellationToken
    )
    {
        await CleanupConnectionAsync("rebuild");

        var query = new Dictionary<string, string?>
        {
            { RpcConstants.CONNECTION_CORRELATION_ID_KEY, attempt.AttemptId },
            { RpcConstants.CONNECTION_CLIENT_NAME_KEY, attempt.ClientName },
            { RpcConstants.CONNECTION_CLIENT_VERSION_KEY, attempt.ClientVersion },
        };
        if (!string.IsNullOrWhiteSpace(attempt.GroupId))
        {
            query[RpcConstants.CONNECTION_GROUP_KEY] = attempt.GroupId;
        }

        var url = QueryHelpers.AddQueryString(_context.Url, query);

        Connection = CreateConnection(url);
        Connection.Reconnected += HandleReconnected;
        Connection.Reconnecting += HandleReconnecting;
        Connection.Closed += HandleClosed;
        foreach (var registerProcedure in Procedures)
        {
            registerProcedure(Connection);
        }
    }

    protected override async Task StartConnectionCoreAsync(
        ConnectionAttemptContext attempt,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "SignalR connect attempt {AttemptId} entering StartAsync for {HubPath}.",
            attempt.AttemptId,
            attempt.HubPath
        );
        _currentConnectCancellationToken = cancellationToken;
        await StartConnectionAsync(Connection!, cancellationToken);
    }

    protected override async Task CleanupConnectionAsync(string reason)
    {
        if (Connection == null)
        {
            return;
        }

        var connection = Connection;
        Connection = null;

        connection.Reconnected -= HandleReconnected;
        connection.Reconnecting -= HandleReconnecting;
        connection.Closed -= HandleClosed;

        try
        {
            if (connection.State != HubConnectionState.Disconnected)
            {
                await connection.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR StopAsync failed during {Reason}.", reason);
        }

        try
        {
            await DisposeConnectionAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR cleanup failed during {Reason}.", reason);
        }
    }

    protected override string GetConnectStartedMessage(ConnectionAttemptContext connectionContext)
    {
        return $"SignalR connect attempt started ({connectionContext.AttemptId}).";
    }

    protected override string? GetConnectedMessage(ConnectionAttemptContext connectionContext)
    {
        return $"SignalR connected ({connectionContext.AttemptId}). ConnectionId: {Connection?.ConnectionId ?? "n/a"}";
    }

    protected override string GetTimedOutMessage(ConnectionAttemptContext connectionContext)
    {
        return $"SignalR connect timed out after {_context.ConnectTimeoutSeconds} seconds ({connectionContext.AttemptId}).";
    }

    protected override string GetCanceledMessage(ConnectionAttemptContext connectionContext)
    {
        return $"SignalR connect attempt canceled ({connectionContext.AttemptId}).";
    }

    protected override string GetFailedMessage(ConnectionAttemptContext connectionContext)
    {
        return $"SignalR connect attempt failed ({connectionContext.AttemptId}).";
    }

    protected override string GetDisconnectedMessage(string reason)
    {
        return "SignalR disconnected.";
    }

    protected override Task OnConnectStartingAsync(ConnectionAttemptContext attempt)
    {
        _currentAttempt = attempt;
        _logger.LogInformation(
            "SignalR connect attempt {AttemptId} started. Hub {HubPath}, Host {Host}, Group {GroupId}, Client {ClientName}, Version {ClientVersion}, TimeoutSeconds {TimeoutSeconds}.",
            attempt.AttemptId,
            attempt.HubPath,
            attempt.Host,
            attempt.GroupId,
            attempt.ClientName,
            attempt.ClientVersion,
            _context.ConnectTimeoutSeconds
        );
        return Task.CompletedTask;
    }

    protected override Task OnConnectedAsync(ConnectionAttemptContext attempt)
    {
        _lastSuccessfulGroupId = attempt.GroupId;
        _logger.LogInformation(
            "SignalR connect attempt {AttemptId} succeeded. Hub {HubPath}, Group {GroupId}, ConnectionId {ConnectionId}.",
            attempt.AttemptId,
            attempt.HubPath,
            attempt.GroupId,
            Connection?.ConnectionId
        );
        return Task.CompletedTask;
    }

    protected override Task OnTimedOutAsync(ConnectionAttemptContext attempt, OperationCanceledException exception)
    {
        _logger.LogWarning(
            exception,
            "SignalR connect attempt {AttemptId} timed out after {TimeoutSeconds} seconds. Hub {HubPath}, Group {GroupId}, Client {ClientName}, Version {ClientVersion}.",
            attempt.AttemptId,
            _context.ConnectTimeoutSeconds,
            attempt.HubPath,
            attempt.GroupId,
            attempt.ClientName,
            attempt.ClientVersion
        );
        return Task.CompletedTask;
    }

    protected override Task OnCanceledAsync(ConnectionAttemptContext attempt, OperationCanceledException exception)
    {
        _logger.LogInformation(
            exception,
            "SignalR connect attempt {AttemptId} was canceled. Hub {HubPath}, Group {GroupId}.",
            attempt.AttemptId,
            attempt.HubPath,
            attempt.GroupId
        );
        return Task.CompletedTask;
    }

    protected override Task OnFailedAsync(ConnectionAttemptContext attempt, Exception exception)
    {
        _logger.LogError(
            exception,
            "SignalR connect attempt {AttemptId} failed. Hub {HubPath}, Group {GroupId}, Client {ClientName}, Version {ClientVersion}.",
            attempt.AttemptId,
            attempt.HubPath,
            attempt.GroupId,
            attempt.ClientName,
            attempt.ClientVersion
        );
        _notifier.Error(exception);
        return Task.CompletedTask;
    }

    protected override Task OnConnectFinishedAsync(ConnectionAttemptContext connectionContext)
    {
        _currentConnectCancellationToken = CancellationToken.None;
        _currentAttempt = null;
        return Task.CompletedTask;
    }

    protected override Task OnActiveConnectInterruptedAsync(Exception exception, string reason)
    {
        _logger.LogDebug(exception, "SignalR connect task finished with an error during {Reason}.", reason);
        return Task.CompletedTask;
    }

    protected override TimeSpan? GetConnectTimeout(ConnectionAttemptContext connectionContext)
    {
        return TimeSpan.FromSeconds(_context.ConnectTimeoutSeconds);
    }

    public void RaiseError(Exception exception, string? procedure, params object?[] arguments)
    {
        var message =
            procedure == null
                ? $"RpcClient error : {exception.Message}"
                : $"RpcClient error in '{procedure}': {exception.Message}";
        _logger.LogError(exception, "{Message}", message);
        var error = new RpcError(exception, procedure, arguments);
        Error?.Invoke(this, error);
    }

    public virtual async Task Connect(string groupId)
    {
        var attempt = CreateAttempt(groupId);
        await ConnectAsync(attempt, CancellationToken.None);
    }

    public virtual async Task Connect()
    {
        var attempt = CreateAttempt(_lastSuccessfulGroupId);
        await ConnectAsync(attempt, CancellationToken.None);
    }

    public virtual async Task Disconnect()
    {
        _lastSuccessfulGroupId = null;
        _currentAttempt = null;
        await DisconnectAsync("manual disconnect");
    }

    async Task<string?> ResolveAccessToken()
    {
        if (_accessTokenProvider == null)
        {
            return null;
        }

        var attempt = _currentAttempt;
        var tokenStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "SignalR token resolution started for attempt {AttemptId}. Hub {HubPath}.",
            attempt?.AttemptId ?? "n/a",
            attempt?.HubPath ?? _context.HubPattern
        );

        var token = await _accessTokenProvider.Get().WaitAsync(_currentConnectCancellationToken);

        tokenStopwatch.Stop();
        _logger.LogInformation(
            "SignalR token resolution completed for attempt {AttemptId} in {ElapsedMilliseconds} ms. HasToken: {HasToken}.",
            attempt?.AttemptId ?? "n/a",
            tokenStopwatch.ElapsedMilliseconds,
            !string.IsNullOrWhiteSpace(token)
        );
        return token;
    }

    Task HandleReconnected(string? connectionId)
    {
        RaiseConnected($"SignalR automatically reconnected: {connectionId}");
        _logger.LogInformation(
            "SignalR automatically reconnected. ConnectionId {ConnectionId}, Group {GroupId}.",
            connectionId,
            _lastSuccessfulGroupId
        );
        return Task.CompletedTask;
    }

    Task HandleReconnecting(Exception? exception)
    {
        RaiseReconnecting($"SignalR automatically reconnecting: {exception?.Message ?? "something went wrong"}");
        _logger.LogWarning(
            exception,
            "SignalR is reconnecting. Hub {HubPath}, Group {GroupId}.",
            _context.HubPattern,
            _lastSuccessfulGroupId
        );
        return Task.CompletedTask;
    }

    Task HandleClosed(Exception? exception)
    {
        RaiseDisconnected();
        RaiseInfo("SignalR connection closed.");
        if (exception == null)
        {
            _logger.LogInformation(
                "SignalR connection closed cleanly. Hub {HubPath}, Group {GroupId}.",
                _context.HubPattern,
                _lastSuccessfulGroupId
            );
        }
        else
        {
            _logger.LogWarning(
                exception,
                "SignalR connection closed with an error. Hub {HubPath}, Group {GroupId}.",
                _context.HubPattern,
                _lastSuccessfulGroupId
            );
        }
        return Task.CompletedTask;
    }

    static RpcSettings Validate(RpcSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.HubPattern))
        {
            throw new Exception(
                $"Invalid SignalR configuration - Host: '{settings.Host}', Pattern: '{settings.HubPattern}'"
            );
        }

        if (settings.ConnectTimeoutSeconds <= 0)
        {
            throw new Exception(
                $"Invalid SignalR configuration - ConnectTimeoutSeconds: '{settings.ConnectTimeoutSeconds}'"
            );
        }

        return settings;
    }

    static bool IsHubConnectionActive(HubConnection? connection)
    {
        return connection?.State
            is HubConnectionState.Connected
                or HubConnectionState.Connecting
                or HubConnectionState.Reconnecting;
    }

    ConnectionAttemptContext CreateAttempt(string? groupId)
    {
        var effectiveGroupId = string.IsNullOrWhiteSpace(groupId) ? _lastSuccessfulGroupId : groupId;
        var attemptId = Guid.NewGuid().ToString("N");
        var clientName = ResolveClientName();
        var clientVersion = ResolveClientVersion();
        var uri = new Uri(_context.Url);

        return new ConnectionAttemptContext
        {
            AttemptId = attemptId,
            Host = uri.Authority,
            HubPath = uri.AbsolutePath,
            GroupId = effectiveGroupId,
            ClientName = clientName,
            ClientVersion = clientVersion,
        };
    }

    string ResolveClientName()
    {
        if (!string.IsNullOrWhiteSpace(_context.ClientName))
        {
            return _context.ClientName;
        }

        if (_context.HubPattern.Equals("judge-hub", StringComparison.OrdinalIgnoreCase))
        {
            return "Judge";
        }

        if (_context.HubPattern.Equals("witness-hub", StringComparison.OrdinalIgnoreCase))
        {
            return "Witness";
        }

        return Assembly.GetEntryAssembly()?.GetName().Name ?? "UnknownClient";
    }

    string ResolveClientVersion()
    {
        if (!string.IsNullOrWhiteSpace(_context.AppVersion))
        {
            return _context.AppVersion;
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        var informationalVersion = entryAssembly
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return entryAssembly?.GetName().Version?.ToString()
            ?? typeof(SignalRSocket).Assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    public sealed class ConnectionAttemptContext
    {
        public string AttemptId { get; init; } = default!;
        public string Host { get; init; } = default!;
        public string HubPath { get; init; } = default!;
        public string? GroupId { get; init; }
        public string ClientName { get; init; } = default!;
        public string ClientVersion { get; init; } = default!;
    }

    sealed class ExistingLoggerFactoryProvider : ILoggerProvider
    {
        readonly ILoggerFactory _loggerFactory;

        public ExistingLoggerFactoryProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }

        public void Dispose() { }
    }
}
