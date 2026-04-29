using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Core;

public interface IEnduranceEventService
{
    Task<IEnumerable<EnduranceEvent>> GetActive();
    Task<IEnumerable<EnduranceEvent>> GetPast();
}
