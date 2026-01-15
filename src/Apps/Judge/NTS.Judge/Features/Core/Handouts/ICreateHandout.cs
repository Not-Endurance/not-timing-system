using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Features.Core.Handouts;

public interface ICreateHandout
{
    Task Create(int number);
    Task<IEnumerable<Combination>> GetCombinations();
}
