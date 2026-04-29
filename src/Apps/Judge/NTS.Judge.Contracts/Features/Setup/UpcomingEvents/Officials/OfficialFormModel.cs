using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Contracts.Features.Setup.UpcomingEvents.Officials;

public record OfficialFormModel : KrudFormModel<Official>
{
    public OfficialFormModel()
    {
#if DEBUG
        Name = "Pesho Goshov";
        Role = OfficialRole.GroundJuryPresident;
#endif
    }

    public string? Name { get; set; }
    public OfficialRole Role { get; set; } = OfficialRole.Steward;
    public User? User { get; set; }

    protected override Official MapTo()
    {
        var names = ConvertName(Name);
        return new Official(names, Role, Id, User);
    }

    public override void MapFrom(Official official)
    {
        Id = official.Id;
        Name = official.Person;
        Role = official.Role;
        User = official.User;
    }

    Person? ConvertName(string? combined)
    {
        return combined == null ? null : new Person(combined.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    }
}
