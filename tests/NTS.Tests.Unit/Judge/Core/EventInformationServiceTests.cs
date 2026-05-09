using System.Linq.Expressions;
using NTS.Application.Core;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Tests.Core;

public class EventInformationServiceTests
{
    [Fact]
    public async Task AddActiveEvent_WhenInvoked_MarksEventAsActiveImmediately()
    {
        var service = new EventInformationService(new TestEventInformationRepository([]));
        var configureEvent = CreateConfigureEvent(7);

        await service.Load();

        Assert.False(service.IsActive(configureEvent));

        service.Add(CreateEventInformation(7));

        Assert.True(service.IsActive(configureEvent));
    }

    [Fact]
    public async Task RemoveActiveEvent_WhenInvoked_ClearsMatchingEventImmediately()
    {
        var service = new EventInformationService(new TestEventInformationRepository([CreateEventInformation(7)]));
        var configureEvent = CreateConfigureEvent(7);

        await service.Load();

        Assert.True(service.IsActive(configureEvent));

        service.Remove(7);

        Assert.False(service.IsActive(configureEvent));
    }

    static ConfigureEvent CreateConfigureEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new ConfigureEvent("Event", "Sofia", country, null, null, null, [], [], [], [], id);
    }

    static EventInformation CreateEventInformation(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EventInformation(
            country,
            "Event",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class TestEventInformationRepository : IEventInformationRepository
    {
        readonly IReadOnlyList<EventInformation> _events;

        public TestEventInformationRepository(IReadOnlyList<EventInformation> events)
        {
            _events = events;
        }

        public Task<IEnumerable<EventInformation>> ReadActive()
        {
            return Task.FromResult<IEnumerable<EventInformation>>(_events);
        }

        public Task<IEnumerable<EventInformation>> ReadPast()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task<EventInformation> Start(int configureEventId)
        {
            throw new NotImplementedException();
        }

        public Task Reset()
        {
            throw new NotImplementedException();
        }

        public Task Deactivate()
        {
            throw new NotImplementedException();
        }

        public Task Create(EventInformation item)
        {
            throw new NotImplementedException();
        }

        public Task<EventInformation?> Read(Expression<Func<EventInformation, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task<EventInformation?> Read(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<EventInformation>> ReadMany()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<EventInformation>> ReadMany(Expression<Func<EventInformation, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task Update(EventInformation item)
        {
            throw new NotImplementedException();
        }

        public Task Delete(EventInformation item)
        {
            throw new NotImplementedException();
        }

        public Task Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMany(Expression<Func<EventInformation, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMany(IEnumerable<EventInformation> items)
        {
            throw new NotImplementedException();
        }
    }
}
