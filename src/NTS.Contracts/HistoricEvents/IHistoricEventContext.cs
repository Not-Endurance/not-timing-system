using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.HistoricEvents;

public interface IHistoricEventContext : IStatefulService
{
    EventInformation? Event { get; }
    Guid EventId { get; }
}
