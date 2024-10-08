using Not.Blazor.Ports;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Loops;

public class LoopFormModel : IFormModel<Loop>
{
    public LoopFormModel()
    {
        // mock data for testing
        Distance = 20;
    }

    public int Id { get; set; }
    public double Distance { get; set; }

    public void FromEntity(Loop entity)
    {
        Id = entity.Id;
        Distance = entity.Distance;
    }
}
