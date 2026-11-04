using NoTiming.Domain.Core.Aggregates.Participations.Entities;

namespace NoTiming.Judge.Contracts.Features.Core.Handouts;

public interface ICreateHandout
{
    Task Create(int number);
    Task<IEnumerable<Combination>> GetCombinations();
}
