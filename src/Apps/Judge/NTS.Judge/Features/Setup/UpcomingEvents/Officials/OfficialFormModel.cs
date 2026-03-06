using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

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

    protected override Official MapTo()
    {
        var names = ConvertName(Name);
        return new Official(names, Role, Id);
    }

    public override void MapFrom(Official official)
    {
        Id = official.Id;
        Name = official.Person;
        Role = official.Role;
    }

    Person? ConvertName(string? combined)
    {
        return combined == null ? null : new Person(combined.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    }
}
