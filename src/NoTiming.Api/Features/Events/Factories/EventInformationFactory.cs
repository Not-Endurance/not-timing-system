using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Events.Factories;

public static class EventInformationFactory
{
    /// <summary>
    /// The Event as it starts from its Setup. It keeps the Tenant of the Setup, and the rules of
    /// the Regional competitions are those of its Tenant at this moment, copied: the Event keeps them whatever the Tenant
    /// edits later (ADR-0012). An Event started without any has none.
    /// </summary>
    public static EventInformation Create(
        NTS.Domain.Setup.Aggregates.ConfigureEvent setupEvent,
        RegionalRules? regionalRules = null
    )
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
            regionalRules: regionalRules,
            tenantId: setupEvent.TenantId
        );
        return eventInformation;
    }
}
