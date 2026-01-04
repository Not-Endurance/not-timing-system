using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Start.Factories;

public static class OfficialFactory
{
    public static Official Create(Domain.Setup.Aggregates.Official official)
    {
        var coreOfficial = new Official(official.Person, official.Role);
        return coreOfficial;
    }
}
