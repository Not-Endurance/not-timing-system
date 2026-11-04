using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Contracts.PastEvents;

public interface IPastEventContext : IStatefulService
{
    EventInformation? Event { get; }
    int EventId { get; }
}
