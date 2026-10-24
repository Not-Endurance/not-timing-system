using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.PastEvents;

public interface IPastEventContext : IStatefulService
{
    EventInformation? Event { get; }
    Guid EventId { get; }
}
