using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;
using NoTiming.Application.Contracts.Shared;

namespace NoTiming.Application.Contracts.Setup.Models;

public class OfficialModel : PersistedModel
{
    public static OfficialModel MapFrom(Official official)
    {
        return new OfficialModel
        {
            DomainVersion = official.DomainVersion,
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
        return new Official(Name, NameEnglish, Role, Id, User?.MapToEntity(), storedDomainVersion: DomainVersion);
    }
}
