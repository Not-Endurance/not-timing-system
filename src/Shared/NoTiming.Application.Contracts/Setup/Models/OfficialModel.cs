using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class OfficialModel
{
    public static OfficialModel MapFrom(Official official)
    {
        return new OfficialModel
        {
            Id = official.Id,
            Name = official.Name,
            NameEnglish = official.NameEnglish,
            Role = official.Role,
            User = official.User == null ? null : UserModel.From(official.User),
        };
    }

    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string? NameEnglish { get; init; }
    public OfficialRole Role { get; init; } = default!;
    public UserModel? User { get; init; }

    public Official MapToEntity()
    {
        return new Official(Name, NameEnglish, Role, Id, User?.MapToEntity());
    }
}
