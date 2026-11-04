using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Contracts.Core;

public interface IEventInformationService
{
    Task<IEnumerable<EventInformation>> GetActive();
    Task<IEnumerable<EventInformation>> GetPast();
}
