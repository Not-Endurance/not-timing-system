using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Factories;

public static class OfficialFactory
{
    public static Official Create(Domain.Setup.Aggregates.Official official)
    {
        var coreOfficial = new Official(official.Person, official.Role);
        return coreOfficial;
    }
}
