using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Contracts.Features.Console.Setup;

public record HorseFormModel : KrudFormModel<Horse>
{
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
