using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC.SignalR;
using NTS.Application.Core;
using NTS.Application.Factories;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.REST;
using CoreOfficial = NTS.Domain.Core.Aggregates.Official;
using SetupCompetition = NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition;

namespace NTS.Judge.Tests.Core;

public class EventScopingTests
{
    [Fact]
    public void OfficialModel_RoundTrip_PreservesEventId()
    {
        var official = new CoreOfficial(
            new Person(["John", "Doe"]),
            OfficialRole.GroundJuryPresident,
            eventId: 42,
            id: 5
        );

        var model = OfficialModel.MapFrom(official);
        var restored = model.MapToEntity();

        Assert.Equal(42, model.EventId);
        Assert.Equal(42, restored.EventId);
    }

    [Fact]
    public void EnduranceEventFactory_Create_SetsEventIdFromUpcomingEvent()
    {
        var setupEvent = CreateUpcomingEvent(77);

        var result = EnduranceEventFactory.Create(setupEvent);

        Assert.Equal(77, result.EventId);
        Assert.Equal(77, result.Id);
    }

    [Fact]
    public void EventScopedRepository_ResolveEndpoint_UsesCurrentEventId()
    {
        var context = new TestSocketContext { Event = CreateUpcomingEvent(12) };
        var repository = new TestEventScopedRepository(context);

        var endpoint = repository.GetScopedEndpoint();

        Assert.Equal("events/12/officials", endpoint);
    }

    [Fact]
    public void EventScopedRepository_ResolveEndpoint_ThrowsWhenEventIsMissing()
    {
        var context = new TestSocketContext();
        var repository = new TestEventScopedRepository(context);

        Assert.Throws<InvalidOperationException>(() => repository.GetScopedEndpoint());
    }

    static UpcomingEvent CreateUpcomingEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var competition = new SetupCompetition(
            "Competition",
            CompetitionType.Qualification,
            CompetitionRuleset.Regional,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            [],
            [],
            1
        );

        return new UpcomingEvent("Event", "Sofia", country, null, null, null, [competition], [], [], [], id);
    }

    sealed class TestEventScopedRepository : EventScopedApiRepository<CoreOfficial, OfficialModel>
    {
        public TestEventScopedRepository(INtsSocketContext socketContext)
            : base("officials", null!, BuildProvider(socketContext)) { }

        public string GetScopedEndpoint()
        {
            return ResolveEndpoint();
        }

        static IServiceProvider BuildProvider(INtsSocketContext socketContext)
        {
            var services = new ServiceCollection();
            services.AddSingleton(socketContext);
            return services.BuildServiceProvider();
        }
    }

    sealed class TestSocketContext : INtsSocketContext
    {
        public bool IsConnected => Event != null;
        public SocketConnectionStatus Status => IsConnected
            ? SocketConnectionStatus.Connected
            : SocketConnectionStatus.Disconnected;
        public UpcomingEvent? Event { get; set; }
    }
}
