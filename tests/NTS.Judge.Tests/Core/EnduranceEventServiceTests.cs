using System.Linq.Expressions;
using NTS.Application.Core;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Tests.Core;

public class EnduranceEventServiceTests
{
    [Fact]
    public async Task AddActiveEvent_WhenInvoked_MarksEventAsActiveImmediately()
    {
        var service = new EnduranceEventService(new TestEnduranceEventRepository([]));
        var upcomingEvent = CreateUpcomingEvent(7);

        await service.Load();

        Assert.False(service.IsActive(upcomingEvent));

        service.Add(CreateEnduranceEvent(7));

        Assert.True(service.IsActive(upcomingEvent));
    }

    [Fact]
    public async Task RemoveActiveEvent_WhenInvoked_ClearsMatchingEventImmediately()
    {
        var service = new EnduranceEventService(new TestEnduranceEventRepository([CreateEnduranceEvent(7)]));
        var upcomingEvent = CreateUpcomingEvent(7);

        await service.Load();

        Assert.True(service.IsActive(upcomingEvent));

        service.Remove(7);

        Assert.False(service.IsActive(upcomingEvent));
    }

    static UpcomingEvent CreateUpcomingEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new UpcomingEvent("Event", "Sofia", country, null, null, null, [], [], [], [], id);
    }

    static EnduranceEvent CreateEnduranceEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
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

    sealed class TestEnduranceEventRepository : IEnduranceEventRepository
    {
        readonly IReadOnlyList<EnduranceEvent> _events;

        public TestEnduranceEventRepository(IReadOnlyList<EnduranceEvent> events)
        {
            _events = events;
        }

        public Task<IEnumerable<EnduranceEvent>> ReadActive()
        {
            return Task.FromResult<IEnumerable<EnduranceEvent>>(_events);
        }

        public Task<EnduranceEvent> Start(int upcomingEventId)
        {
            throw new NotImplementedException();
        }

        public Task Reset()
        {
            throw new NotImplementedException();
        }

        public Task Create(EnduranceEvent item)
        {
            throw new NotImplementedException();
        }

        public Task<EnduranceEvent?> Read(Expression<Func<EnduranceEvent, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task<EnduranceEvent?> Read(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<EnduranceEvent>> ReadMany()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<EnduranceEvent>> ReadMany(Expression<Func<EnduranceEvent, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task Update(EnduranceEvent item)
        {
            throw new NotImplementedException();
        }

        public Task Delete(EnduranceEvent item)
        {
            throw new NotImplementedException();
        }

        public Task Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task Delete(Expression<Func<EnduranceEvent, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task Delete(IEnumerable<EnduranceEvent> items)
        {
            throw new NotImplementedException();
        }
    }
}
