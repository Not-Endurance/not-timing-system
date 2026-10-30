using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Core;

public interface IEventInformationService
{
    Task<IEnumerable<EventInformation>> GetLive();
    Task<IEnumerable<EventInformation>> GetHistoric();
}
