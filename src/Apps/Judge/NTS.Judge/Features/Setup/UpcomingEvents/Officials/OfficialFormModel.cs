using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialFormModel : IFormModel<Official>
{
    public OfficialFormModel()
    {
#if DEBUG
        Name = "Pesho Goshov";
        Role = OfficialRole.GroundJuryPresident;
#endif
    }

    public int? Id { get; set; }
    public string? Name { get; set; }
    public OfficialRole Role { get; set; } = OfficialRole.Steward;

    public void FromEntity(Official official)
    {
        Id = official.Id;
        Name = official.Person;
        Role = official.Role;
    }
}
