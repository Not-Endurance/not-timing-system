using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Factories;

public static class OfficialFactory
{
    public static Official Create(Domain.Setup.Aggregates.ConfigureEvents.Official setupOfficial, int eventId)
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
