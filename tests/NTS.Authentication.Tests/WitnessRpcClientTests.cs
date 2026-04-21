using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Exceptions;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Application.Watcher;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Nexus.Warp.Contracts;
using NTS.Witness.Features.Socket;

namespace NTS.Authentication.Tests;

public class WitnessRpcClientTests
{
    [Fact]
    public async Task PublishSnapshotsAsync_uses_the_connected_event_id()
    {
        var socket = new RecordingSignalRSocket(CreateConnection());
        var client = new RecordingWitnessRpcClient(
            socket,
            new TestSocketContext(CreateEvent(17)),
            new RecordingDomainEventDispatcher()
        );

        await client.PublishSnapshotsAsync(CreateSnapshotGroup());

        Assert.NotNull(client.LastRequest);
        Assert.Equal("17", client.LastRequest!.EnduranceEventId);
        Assert.Single(client.LastRequest.Payload.Entries);
    }

    [Fact]
    public async Task PublishSnapshotsAsync_throws_when_no_event_is_connected()
    {
        var socket = new RecordingSignalRSocket(CreateConnection());
        var client = new RecordingWitnessRpcClient(
            socket,
            new TestSocketContext(null),
            new RecordingDomainEventDispatcher()
        );

        await Assert.ThrowsAsync<GuardException>(() => client.PublishSnapshotsAsync(CreateSnapshotGroup()));
        Assert.Null(client.LastRequest);
    }

    static SnapshotGroup CreateSnapshotGroup()
    {
        return new SnapshotGroup(
            [new NTS.Domain.Watcher.Snapshot(31, new Person(["Test", "Rider"]), new Timestamp(DateTimeOffset.UtcNow))],
            SnapshotType.Arrive
        );
    }

    static HubConnection CreateConnection()
    {
        return new HubConnectionBuilder().WithUrl("https://localhost/witness-hub").Build();
    }

    static EnduranceEvent CreateEvent(int eventId)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Ring",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            eventId
        );
    }

    sealed class RecordingWitnessRpcClient : WitnessRpcClient
    {
        public RecordingWitnessRpcClient(
            SignalRSocket socket,
            INtsSocketContext socketContext,
            IDomainEventDispatcher domainEventDispatcher
        )
            : base(socket, socketContext, domainEventDispatcher) { }

        public WarpRequest<SnapshotGroupModel>? LastRequest { get; private set; }

        protected override Task SendReceiveAsync(WarpRequest<SnapshotGroupModel> request)
        {
            LastRequest = request;
            return Task.CompletedTask;
        }
    }

    sealed class RecordingSignalRSocket : SignalRSocket
    {
        public RecordingSignalRSocket(HubConnection connection)
            : base(
                Options.Create(new RpcSettings { Host = "https://localhost", HubPattern = "witness-hub" }),
                new TestNotifier()
            )
        {
            typeof(SignalRSocket).GetProperty(nameof(Connection))!.SetValue(this, connection);
        }
    }

    sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task Dispatch(Not.Domain.Abstractions.IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Dispatch(Not.Domain.Aggregate aggregate, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestSocketContext : INtsSocketContext
    {
        public TestSocketContext(EnduranceEvent? @event)
        {
            Event = @event;
        }

        public bool IsConnected => Event != null;
        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; }
    }

    sealed class TestNotifier : INotifier
    {
        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }

        public void Error(string message) { }

        public void Error(Exception ex) { }
    }
}
