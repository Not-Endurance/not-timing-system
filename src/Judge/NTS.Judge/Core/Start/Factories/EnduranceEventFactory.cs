using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Core.Start.Factories;

public static class EnduranceEventFactory
{
    public static EnduranceEvent Create(Domain.Setup.Aggregates.UpcomingEvent setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            throw new DomainException("Cannot start - Competitions aren't configured");
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.Start).ToList();
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        // TODO: fix city and pla
        var enduranceEvent = new EnduranceEvent(
            setupEvent.Id,
            setupEvent.Country,
            setupEvent.Place,
            setupEvent.Place,
            startDate,
            endDate,
            setupEvent.ShowFeiId,
            setupEvent.FeiId,
            setupEvent.FeiEventCode
        );
        return enduranceEvent;
    }
}
