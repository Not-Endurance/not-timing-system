using NC.Application.Behinds.Adapters;
using NC.Observables.Structures;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Judge.Contracts.Features.Core.Rankings;

public interface IRankingContext : IStatefulService
{
    Ranking Current { get; }
    ObservableList<Ranking> Rankings { get; }
}
