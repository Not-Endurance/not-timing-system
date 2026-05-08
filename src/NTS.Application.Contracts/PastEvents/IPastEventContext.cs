using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.PastEvents;

public interface IPastEventContext : IStatefulService
{
    EventInformation? Event { get; }
    int EventId { get; }
}
