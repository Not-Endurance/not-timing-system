using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects.Documents;
using NoTiming.Domain.Core.Objects.Startlists;

namespace NoTiming.Application.Contracts.PastEvents;

public interface IPastEventService : IPastEventContext
{
    IReadOnlyList<EventInformation> Events { get; }
    IReadOnlyList<Ranking> Rankings { get; }
    Ranking? CurrentRanking { get; }
    IReadOnlyDictionary<int, IReadOnlyList<Starter>> StartlistHistoryByStage { get; }
    ResultsDocument? Document { get; }

    Task LoadEvent(int eventId);
    ResultsDocument? CreateDocument(Ranking ranking);
    void Select(Ranking ranking);
}
