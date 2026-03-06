using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Horses;

public record HorseFormModel : KrudFormModel<Horse>
{
    public HorseFormModel()
    {
#if DEBUG
        Name = "Хан Аспарух";
#endif
    }

    public string? FeiId { get; set; }
    public string? Name { get; set; }

    protected override Horse MapTo()
    {
        return new(Name, FeiId, Id);
    }

    public override void MapFrom(Horse horse)
    {
        Id = horse.Id;
        FeiId = horse.FeiId;
        Name = horse.Name;
    }
}
