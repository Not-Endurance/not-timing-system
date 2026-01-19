using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Start.Factories;

public static class OfficialFactory
{
    public static Official Create(Domain.Setup.Aggregates.UpcomingEvents.Official setupOfficial)
    {
        return new Official(setupOfficial.Id, setupOfficial.Person, setupOfficial.Role);
    }
}
