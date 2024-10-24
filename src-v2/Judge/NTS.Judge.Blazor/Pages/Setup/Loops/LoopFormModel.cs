﻿using Not.Blazor.Ports;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Loops;

public class LoopFormModel : IFormModel<Loop>
{
    public LoopFormModel()
    {
#if DEBUG
        Distance = 20;
#endif
    }

    public int Id { get; set; }
    public double Distance { get; set; }

    public void FromEntity(Loop entity)
    {
        Id = entity.Id;
        Distance = entity.Distance;
    }
}
