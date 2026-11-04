using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Judge.Contracts.Features.Core.Handouts;

public interface ICreateHandout
{
    Task Create(int number);
    Task<IEnumerable<Combination>> GetCombinations();
}
