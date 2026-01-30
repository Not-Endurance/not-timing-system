using Not.Application.Behinds.Adapters;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRankingMenuService : IRankingContext, ISingleton
{
    ObservableList<Ranking> Rankings { get; }
    void Select(Ranking ranking);
    Task Delete(Ranking ranking);
}
