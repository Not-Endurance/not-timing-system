using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.Clubs;
public class ClubFormModel : IFormModel<Club>
{
    public ClubFormModel()
    {
#if DEBUG
        Name = "Конярче";
#endif
    }

    public int? Id { get; set; }
    public string? Name { get; set; }

    public void FromEntity(Club entity)
    {
        Id = entity.Id;
        Name = entity.Name;
    }
}
