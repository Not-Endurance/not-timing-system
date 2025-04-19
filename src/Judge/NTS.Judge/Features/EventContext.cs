using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features;

public class EventContext : IEventContext
{
    public UpcomingEvent? Event { get; set; }
}

public interface IEventContext : ISingleton
{
    public UpcomingEvent? Event { get; }
}
