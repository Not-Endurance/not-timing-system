using Not.Application.RPC;
using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features;

public class EventContext : IEventContext, IRpcMetadata
{
    string? IRpcMetadata.ConnectionGroupKey => Event?.Id.ToString();
    public UpcomingEvent? Event { get; set; }
}

public interface IEventContext : ISingleton
{
    public UpcomingEvent? Event { get; }
}
