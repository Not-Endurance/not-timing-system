using Not.Application.Behinds.Adapters;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Contracts.Features.Core.Rankings;

public interface IRankingContext : IStatefulService
{
    Ranking Current { get; }
    ObservableList<Ranking> Rankings { get; }
}
