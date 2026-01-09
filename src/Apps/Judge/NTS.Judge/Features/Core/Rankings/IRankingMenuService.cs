using Not.Blazor.Ports;
using Not.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRankingMenuService : INObservable
{
    Ranking? SelectedRanking { get; }
    ObservableList<Ranking> Rankings { get; }
    Task Select(Ranking ranking);
    Task Delete(Ranking ranking);
}
