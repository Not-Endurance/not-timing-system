using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Contracts.Features.Setup.Horses;

public record HorseFormModel : KrudFormModel<Horse>
{
    public HorseFormModel()
    {
#if DEBUG
        Name = "Хан Аспарух";
        NameEnglish = "Khan Asparuh";
#endif
    }

    public string? FeiId { get; set; }
    public string? Name { get; set; }
    public string? NameEnglish { get; set; }

    protected override Horse MapTo()
    {
        return new(Name, NameEnglish, FeiId, Id);
    }

    public override void MapFrom(Horse horse)
    {
        Id = horse.Id;
        FeiId = horse.FeiId;
        Name = horse.Name;
        NameEnglish = horse.NameEnglish;
    }
}
