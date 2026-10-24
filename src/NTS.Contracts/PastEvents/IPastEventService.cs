using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Contracts.PastEvents;

public interface IPastEventService : IPastEventContext
{
    IReadOnlyList<EventInformation> Events { get; }
    IReadOnlyList<Ranking> Rankings { get; }
    Ranking? CurrentRanking { get; }
    IReadOnlyDictionary<int, IReadOnlyList<Starter>> StartlistHistoryByStage { get; }
    ResultsDocument? Document { get; }

    Task LoadEvent(Guid eventId);
    ResultsDocument? CreateDocument(Ranking ranking);
    void Select(Ranking ranking);
}
