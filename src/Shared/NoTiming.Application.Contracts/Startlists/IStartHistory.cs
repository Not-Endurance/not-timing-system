using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Objects.Startlists;

namespace NoTiming.Application.Contracts.Startlists;

public interface IStartHistory : IStatefulService
{
    IReadOnlyList<Starter> History { get; }
    IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; }
}
