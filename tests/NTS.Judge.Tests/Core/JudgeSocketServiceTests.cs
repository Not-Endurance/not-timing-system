using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Notify;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Judge.Features.Socket;

namespace NTS.Judge.Tests.Core;

public class JudgeSocketServiceTests
{
    [Fact]
    public async Task Connect_DispatchesEventConnected()
    {
        var dispatcher = new RecordingDomainEventDispatcher();
        var socket = new RecordingRpcSocket();
        var service = new JudgeSocketService(socket, new TestNotifier(), dispatcher);
        var enduranceEvent = CreateEvent(14);

        await service.Connect(enduranceEvent);

        Assert.Equal("14", socket.LastGroupId);
        Assert.Equal([nameof(EventConnected)], dispatcher.Events);
        Assert.Equal(14, service.Event?.Id);
    }

    [Fact]
    public async Task Disconnect_DispatchesEventDisconnected()
    {
        var dispatcher = new RecordingDomainEventDispatcher();
        var socket = new RecordingRpcSocket();
        var service = new JudgeSocketService(socket, new TestNotifier(), dispatcher);

        await service.Connect(CreateEvent(21));
        await service.Disconnect();

        Assert.Equal([nameof(EventConnected), nameof(EventDisconnected)], dispatcher.Events);
        Assert.Null(service.Event);
    }

    [Fact]
    public async Task Connect_WhenSocketDoesNotConnect_DoesNotDispatchOrSetEvent()
    {
        var dispatcher = new RecordingDomainEventDispatcher();
        var socket = new RecordingRpcSocket { ShouldConnect = false };
        var service = new JudgeSocketService(socket, new TestNotifier(), dispatcher);

        await service.Connect(CreateEvent(34));

        Assert.Empty(dispatcher.Events);
        Assert.Null(service.Event);
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class RecordingRpcSocket : IRpcSocket
    {
        public event EventHandler<SocketConnectionStatus>? ServerConnectionChanged;
        public event EventHandler<string>? ServerConnectionInfo;
        public event EventHandler<RpcError>? Error;

        public HubConnection? Connection => null;
        public bool IsConnected { get; private set; }
        public string? LastGroupId { get; private set; }
        public bool ShouldConnect { get; set; } = true;

        public Task Connect()
        {
            return Connect("");
        }

        public Task Connect(string groupId)
        {
            LastGroupId = groupId;
            if (ShouldConnect)
            {
                IsConnected = true;
                ServerConnectionChanged?.Invoke(this, SocketConnectionStatus.Connected);
            }
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            IsConnected = false;
            ServerConnectionChanged?.Invoke(this, SocketConnectionStatus.Disconnected);
            return Task.CompletedTask;
        }

        public void RaiseError(Exception exception, string? procedure, params object?[] arguments)
        {
            Error?.Invoke(this, new RpcError(exception, procedure, arguments));
        }
    }

    sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<string> Events { get; } = [];

        public Task Dispatch(Not.Domain.Abstractions.IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            Events.Add(@event.GetType().Name);
            return Task.CompletedTask;
        }

        public Task Dispatch(Not.Domain.Aggregate aggregate, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    sealed class TestNotifier : INotifier
    {
        public void Error(string message) { }

        public void Error(Exception ex) { }

        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }
    }
}
