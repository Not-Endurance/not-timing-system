using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Officials;

public class OfficialModel : Document
{
    public OfficialModel(Official official)
        : base(official.Id)
    {
        Names = official.Person.Names;
        Role = official.Role;
    }

    public string[] Names { get; init; }
    public OfficialRole Role { get; init; }
}
