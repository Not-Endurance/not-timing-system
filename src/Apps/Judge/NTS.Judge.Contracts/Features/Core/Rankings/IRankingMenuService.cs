using Not.Injection;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Contracts.Features.Core.Rankings;

public interface IRankingMenuService : IRankingContext, IScoped
{
    void Select(Ranking ranking);
    Task Delete(Ranking ranking);
}
