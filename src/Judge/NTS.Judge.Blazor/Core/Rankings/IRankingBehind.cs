using Not.Blazor.Ports;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Blazor.Core.Rankings;

public interface IRankingBehind : IObservableBehind, ISingleton
{
    Ranklist? Ranklist { get; }
    Task<IEnumerable<Ranking>> GetRankings();
    Task SelectRanking(int id);
    Task Archive();
    Task ExportFei();
}
