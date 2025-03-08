using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.Loops.Dot;

public class LoopFormModel : IFormModel<Loop>
{
    public LoopFormModel()
    {
#if DEBUG
        Distance = 20;
#endif
    }

    public int? Id { get; set; }
    public double? Distance { get; set; }

    public void FromEntity(Loop entity)
    {
        Id = entity.Id;
        Distance = entity.Distance;
    }
}
