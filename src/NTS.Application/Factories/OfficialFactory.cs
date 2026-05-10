using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Factories;

public static class OfficialFactory
{
    public static Official Create(Domain.Setup.Aggregates.ConfigureEvents.Official setupOfficial, int eventId)
    {
        return new Official(setupOfficial.Person, setupOfficial.Role, eventId, userId: setupOfficial.User?.Id);
    }
}
