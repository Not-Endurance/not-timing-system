using Microsoft.Extensions.Options;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;

namespace NTS.Authentication.Tests;

public class SignalRSocketTests
{
    [Fact]
    public async Task Connect_when_token_acquisition_is_canceled_returns_to_disconnected_state()
    {
        var notifier = new RecordingNotifier();
        var socket = new SignalRSocket(
            Options.Create(new RpcSettings { Host = "https://localhost", HubPattern = "witness-hub" }),
            notifier,
            new CancelingAccessTokenProvider()
        );
        var statuses = new List<SocketConnectionStatus>();
        socket.ServerConnectionChanged += (_, status) => statuses.Add(status);

        await socket.Connect("14");

        Assert.Equal([SocketConnectionStatus.Connecting, SocketConnectionStatus.Disconnected], statuses);
        Assert.False(socket.IsConnected);
        Assert.Null(socket.Connection);
        Assert.Empty(notifier.Errors);
    }

    sealed class CancelingAccessTokenProvider : IRpcAccessTokenProvider
    {
        public Task<string?> Get()
        {
            throw new OperationCanceledException("Interactive authentication redirect started.");
        }
    }

    sealed class RecordingNotifier : INotifier
    {
        public List<Exception> Errors { get; } = [];

        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }

        public void Warn(IEnumerable<string> messages) { }

        public void Error(string message) { }

        public void Error(Exception ex)
        {
            Errors.Add(ex);
        }
    }
}
