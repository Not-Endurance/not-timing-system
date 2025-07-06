using Not.Blazor.Ports;
using Not.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Rankings.Menu;

public interface IRankingMenuService : IObservableBehind
{
    Ranking? SelectedRanking { get; }
    ObservableList<Ranking> Rankings { get; }
    Task Select(Ranking ranking);
    Task Delete(Ranking ranking);
}
