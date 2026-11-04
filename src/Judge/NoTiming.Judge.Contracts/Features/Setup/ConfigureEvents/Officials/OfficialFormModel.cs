using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Officials;

public record OfficialFormModel : KrudFormModel<Official>
{
    public OfficialFormModel()
    {
#if DEBUG
        Name = "Pesho Goshov";
        NameEnglish = "Pesho Goshov";
        Role = OfficialRole.GroundJuryPresident;
#endif
    }

    public string? Name { get; set; }
    public string? NameEnglish { get; set; }
    public OfficialRole Role { get; set; } = OfficialRole.Steward;
    public User? User { get; set; }

    protected override Official MapTo()
    {
        return new Official(Name, NameEnglish, Role, Id, User);
    }

    public override void MapFrom(Official official)
    {
        Id = official.Id;
        Name = official.Name;
        NameEnglish = official.NameEnglish;
        Role = official.Role;
        User = official.User;
    }
}
