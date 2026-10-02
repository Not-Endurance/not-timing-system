using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Contracts.Startlists;

public interface IStartHistory : IStatefulService
{
    IReadOnlyList<Starter> History { get; }
    IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; }
}
