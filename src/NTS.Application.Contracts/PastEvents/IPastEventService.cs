using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Contracts.PastEvents;

public interface IPastEventService : IPastEventContext
{
    IReadOnlyList<EnduranceEvent> Events { get; }
    IReadOnlyList<Ranking> Rankings { get; }
    Ranking? CurrentRanking { get; }
    IReadOnlyDictionary<int, IReadOnlyList<Starter>> StartlistHistoryByStage { get; }
    RanklistDocument? Document { get; }

    Task LoadEvent(int eventId);
    void Select(Ranking ranking);
}
