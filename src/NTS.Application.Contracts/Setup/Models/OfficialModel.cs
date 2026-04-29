using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class OfficialModel
{
    public static OfficialModel MapFrom(Official official)
    {
        return new OfficialModel
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
            User = official.User == null ? null : UserModel.From(official.User),
        };
    }

    public int Id { get; init; }
    public string[] Names { get; init; } = [];
    public OfficialRole Role { get; init; } = default!;
    public UserModel? User { get; init; }

    public Official MapToEntity()
    {
        return new Official(Names, Role, Id, User?.MapToEntity());
    }
}
