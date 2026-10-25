using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.Events.Factories;

public static class OfficialFactory
{
    public static Official Create(NTS.Domain.Setup.Aggregates.ConfigureEvents.Official setupOfficial, Guid eventId)
    {
        return new Official(
            setupOfficial.Name,
            setupOfficial.NameEnglish,
            setupOfficial.Role,
            eventId,
            userId: setupOfficial.User?.Id
        );
    }
}
