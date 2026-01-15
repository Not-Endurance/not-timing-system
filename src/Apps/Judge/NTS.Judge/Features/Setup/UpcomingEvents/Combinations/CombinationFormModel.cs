using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationFormModel : IFormModel<Combination>
{
    public CombinationFormModel()
    {
#if DEBUG
        Number = 37;
#endif
    }

    public int? Id { get; set; }
    public int? Number { get; set; }
    public Athlete? Athlete { get; set; }
    public Horse? Horse { get; set; }
    public Tag? Tag { get; set; }

    public void FromEntity(Combination combination)
    {
        Id = combination.Id;
        Number = combination.Number;
        Athlete = combination.Athlete;
        Horse = combination.Horse;
        Tag = combination.Tag;
    }
}
