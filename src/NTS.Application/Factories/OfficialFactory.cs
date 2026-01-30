using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Factories;

public static class OfficialFactory
{
    public static Official Create(Domain.Setup.Aggregates.UpcomingEvents.Official setupOfficial)
    {
        return new Official(setupOfficial.Person, setupOfficial.Role, setupOfficial.Id);
    }
}
