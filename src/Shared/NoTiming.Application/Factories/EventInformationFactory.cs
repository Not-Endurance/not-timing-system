using NC.Domain.Exceptions;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects;
using NoTiming.Domain.Objects;

namespace NoTiming.Application.Factories;

public static class EventInformationFactory
{
    public static EventInformation Create(Domain.Setup.Aggregates.ConfigureEvent setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            throw new DomainException("Cannot start - Competitions aren't configured");
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.Start).ToList();
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        var eventInformation = new EventInformation(
            setupEvent.Country,
            setupEvent.Name,
            setupEvent.Location,
            new EventSpan(startDate, endDate),
            setupEvent.FeiShowId,
            setupEvent.Id,
            isActive: true
        );
        return eventInformation;
    }
}
