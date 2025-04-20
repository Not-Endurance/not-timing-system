using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Officials;

public class OfficialDocument : Document
{
    public static OfficialDocument Create(Official official)
    {
        return new OfficialDocument
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
        };
    }
    
    public static OfficialDocument Create(Domain.Setup.Aggregates.Official official)
    {
        return new OfficialDocument
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
        };
    }

    public string[] Names { get; init; } = [];
    public OfficialRole Role { get; init; } = default!;

    public Official ToCoreDomain()
    {
        return new Official(Id, Names, Role);
    }

    public Domain.Setup.Aggregates.Official ToSetupDomain()
    {
        return new Domain.Setup.Aggregates.Official(Id, Names, Role);
    }
}
