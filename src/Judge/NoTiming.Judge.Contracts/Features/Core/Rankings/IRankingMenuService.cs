using NC.Injection;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Judge.Contracts.Features.Core.Rankings;

public interface IRankingMenuService : IRankingContext, IScoped
{
    void Select(Ranking ranking);
    Task Delete(Ranking ranking);
}
