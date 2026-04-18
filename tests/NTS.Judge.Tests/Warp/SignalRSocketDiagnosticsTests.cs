using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;
using NTS.Judge.Tests.Core.Implementations;

namespace NTS.Judge.Tests.Warp;

public class SignalRSocketDiagnosticsTests
{
    [Fact]
    public async Task Connect_when_start_async_times_out_returns_to_disconnected_state()
    {
        var notifier = new TestNotifier();
        var socket = new TestSignalRSocket(
            Options.Create(
                new RpcSettings
                {
                    Host = "https://localhost",
                    HubPattern = "witness-hub",
                    ConnectTimeoutSeconds = 1,
                    ClientName = "WitnessTests",
                    AppVersion = "9.9.9",
                }
            ),
            notifier
        )
        {
            StartBehavior = cancellationToken => Task.Delay(TimeSpan.FromSeconds(5), cancellationToken),
        };
        var statuses = new List<SocketConnectionStatus>();
        var infoMessages = new List<string>();
        socket.ServerConnectionChanged += (_, status) => statuses.Add(status);
        socket.ServerConnectionInfo += (_, info) => infoMessages.Add(info);

        await socket.Connect("14");

        Assert.Equal([SocketConnectionStatus.Connecting, SocketConnectionStatus.Disconnected], statuses);
        Assert.Contains(infoMessages, x => x.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, socket.StartCallCount);
        Assert.Equal(1, socket.DisposeCallCount);
        Assert.Null(socket.Connection);
        Assert.Empty(notifier.Errors);
    }

    [Fact]
    public async Task Connect_when_called_concurrently_starts_only_one_attempt()
    {
        var socket = new TestSignalRSocket(
            Options.Create(
                new RpcSettings
                {
                    Host = "https://localhost",
                    HubPattern = "witness-hub",
                    ConnectTimeoutSeconds = 5,
                }
            ),
            new TestNotifier()
        )
        {
            StartBehavior = cancellationToken => Task.Delay(100, cancellationToken),
        };

        await Task.WhenAll(socket.Connect("14"), socket.Connect("14"));

        Assert.Equal(1, socket.StartCallCount);
        Assert.Single(socket.CreatedUrls);
    }

    [Fact]
    public async Task Connect_without_group_after_connection_loss_reuses_the_last_successful_group()
    {
        var socket = new TestSignalRSocket(
            Options.Create(
                new RpcSettings
                {
                    Host = "https://localhost",
                    HubPattern = "witness-hub",
                    ClientName = "WitnessTests",
                    AppVersion = "9.9.9",
                }
            ),
            new TestNotifier()
        );

        await socket.Connect("14");
        socket.DropConnection();
        await socket.Connect();

        var reconnectUrl = new Uri(socket.CreatedUrls.Last());
        var reconnectQuery = QueryHelpers.ParseQuery(reconnectUrl.Query);

        Assert.Equal("14", reconnectQuery[RpcConstants.CONNECTION_GROUP_KEY].ToString());
        Assert.False(string.IsNullOrWhiteSpace(reconnectQuery[RpcConstants.CONNECTION_CORRELATION_ID_KEY].ToString()));
        Assert.Equal("WitnessTests", reconnectQuery[RpcConstants.CONNECTION_CLIENT_NAME_KEY].ToString());
        Assert.Equal("9.9.9", reconnectQuery[RpcConstants.CONNECTION_CLIENT_VERSION_KEY].ToString());
    }

    [Fact]
    public async Task Disconnect_disposes_the_connection_and_clears_the_cached_group()
    {
        var socket = new TestSignalRSocket(
            Options.Create(new RpcSettings { Host = "https://localhost", HubPattern = "witness-hub" }),
            new TestNotifier()
        );

        await socket.Connect("14");
        await socket.Disconnect();

        Assert.Null(socket.Connection);
        Assert.Equal(1, socket.DisposeCallCount);

        await socket.Connect();

        var reconnectUrl = new Uri(socket.CreatedUrls.Last());
        var reconnectQuery = QueryHelpers.ParseQuery(reconnectUrl.Query);

        Assert.False(reconnectQuery.ContainsKey(RpcConstants.CONNECTION_GROUP_KEY));
    }

    sealed class TestSignalRSocket : SignalRSocket
    {
        public TestSignalRSocket(IOptions<RpcSettings> options, INotifier notifier)
            : base(options, notifier) { }

        public List<string> CreatedUrls { get; } = [];
        public int StartCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }
        public Func<CancellationToken, Task> StartBehavior { get; init; } = _ => Task.CompletedTask;

        protected override HubConnection CreateConnection(string url)
        {
            CreatedUrls.Add(url);
            return new HubConnectionBuilder().WithUrl(url).Build();
        }

        protected override async Task StartConnectionAsync(
            HubConnection connection,
            CancellationToken cancellationToken
        )
        {
            StartCallCount++;
            await StartBehavior(cancellationToken);
        }

        protected override Task DisposeConnectionAsync(HubConnection connection)
        {
            DisposeCallCount++;
            return Task.CompletedTask;
        }

        public void DropConnection()
        {
            Connection = null;
        }
    }
}
