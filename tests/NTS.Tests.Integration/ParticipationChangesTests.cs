using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoTiming.Api.Features.Live;
using NTS.Contracts;
using NTS.Contracts.Live;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// What a viewer is told when a Participation changes (#623, ADR-0013): one notification per change, naming it, to the
/// clients of its Event. The Api announces after the write it made has succeeded; this is the service it uses.
/// </summary>
public sealed class ParticipationChangesTests : IClassFixture<ApiHostFixture>
{
    static readonly TimeSpan PATIENCE = TimeSpan.FromSeconds(10);
    static readonly Guid AN_EVENT = TestId.Of(7);
    static readonly Guid ANOTHER_EVENT = TestId.Of(8);

    readonly ApiHostFixture _host;

    public ParticipationChangesTests(ApiHostFixture host)
    {
        _host = host;
    }

    [Fact]
    public async Task Each_announcement_is_one_notification_to_the_clients_of_its_Event_and_to_no_other()
    {
        await using var inTheEvent = Connect(AN_EVENT);
        await using var elsewhere = Connect(ANOTHER_EVENT);
        var received = new List<(Guid EventId, Guid ParticipationId)>();
        var missed = new List<(Guid, Guid)>();
        inTheEvent.On<Guid, Guid>(
            nameof(ILiveClientProcedures.ParticipationChanged),
            (eventId, participationId) =>
            {
                lock (received)
                {
                    received.Add((eventId, participationId));
                }
            }
        );
        elsewhere.On<Guid, Guid>(
            nameof(ILiveClientProcedures.ParticipationChanged),
            (eventId, participationId) =>
            {
                lock (missed)
                {
                    missed.Add((eventId, participationId));
                }
            }
        );
        await inTheEvent.StartAsync();
        await elsewhere.StartAsync();
        var changes = _host.Api.Services.GetRequiredService<IParticipationChanges>();

        await changes.AnnounceAsync(AN_EVENT, TestId.Of(1));
        await changes.AnnounceAsync(AN_EVENT, TestId.Of(2));
        await changes.AnnounceAsync(AN_EVENT, TestId.Of(1)); // the same Participation changed twice: two notifications

        await WaitUntil(() => received.Count == 3);
        await Task.Delay(300);
        Assert.Equal(
            [(AN_EVENT, TestId.Of(1)), (AN_EVENT, TestId.Of(2)), (AN_EVENT, TestId.Of(1))],
            received.ToArray()
        );
        Assert.Empty(missed);
    }

    [Fact]
    public async Task A_hub_that_fails_is_logged_and_does_not_fail_the_write_that_is_being_announced()
    {
        var changes = new ParticipationChanges(new HubThatIsDown(), NullLogger<ParticipationChanges>.Instance);

        await changes.AnnounceAsync(AN_EVENT, TestId.Of(1)); // the change is stored; nothing here may turn that into an error
    }

    HubConnection Connect(Guid eventId)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_host.BaseAddress, $"{ApplicationConstants.LIVE_HUB}?connectionGroup={eventId}"))
            .AddNewtonsoftJsonProtocol()
            .Build();
    }

    static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(PATIENCE);
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    sealed class HubThatIsDown : IHubContext<LiveHub, ILiveClientProcedures>
    {
        public IHubClients<ILiveClientProcedures> Clients { get; } = new ClientsThatAreDown();

        public IGroupManager Groups => throw new NotSupportedException();
    }

    sealed class ClientsThatAreDown : IHubClients<ILiveClientProcedures>
    {
        static readonly ILiveClientProcedures DOWN = new ProceduresThatAreDown();

        public ILiveClientProcedures All => DOWN;

        public ILiveClientProcedures AllExcept(IReadOnlyList<string> excludedConnectionIds)
        {
            return DOWN;
        }

        public ILiveClientProcedures Client(string connectionId)
        {
            return DOWN;
        }

        public ILiveClientProcedures Clients(IReadOnlyList<string> connectionIds)
        {
            return DOWN;
        }

        public ILiveClientProcedures Group(string groupName)
        {
            return DOWN;
        }

        public ILiveClientProcedures GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
        {
            return DOWN;
        }

        public ILiveClientProcedures Groups(IReadOnlyList<string> groupNames)
        {
            return DOWN;
        }

        public ILiveClientProcedures User(string userId)
        {
            return DOWN;
        }

        public ILiveClientProcedures Users(IReadOnlyList<string> userIds)
        {
            return DOWN;
        }
    }

    sealed class ProceduresThatAreDown : ILiveClientProcedures
    {
        public Task ParticipationChanged(Guid eventId, Guid participationId)
        {
            throw new InvalidOperationException("The hub is down.");
        }
    }
}
