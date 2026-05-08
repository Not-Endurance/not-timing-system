using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Core;

public interface IEventInformationService
{
    Task<IEnumerable<EventInformation>> GetActive();
    Task<IEnumerable<EventInformation>> GetPast();
}
