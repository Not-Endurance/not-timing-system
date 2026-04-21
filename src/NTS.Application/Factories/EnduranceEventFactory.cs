using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;

namespace NTS.Application.Factories;

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

        var enduranceEvent = new EnduranceEvent(
            setupEvent.Country,
            setupEvent.Name,
            setupEvent.Location,
            new EventSpan(startDate, endDate),
            setupEvent.ShowFeiId,
            setupEvent.FeiId,
            setupEvent.FeiEventCode,
            setupEvent.Id
        );
        return enduranceEvent;
    }
}
