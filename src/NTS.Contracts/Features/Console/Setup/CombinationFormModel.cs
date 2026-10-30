using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Features.Console.Setup;

public record CombinationFormModel : KrudFormModel<Combination>
{
    public int? Number { get; set; }
    public Athlete? Athlete { get; set; }
    public Horse? Horse { get; set; }

    protected override Combination MapTo()
    {
        return new(Number, Athlete, Horse, Id);
    }

    public override void MapFrom(Combination combination)
    {
        Id = combination.Id;
        Number = combination.Number;
        Athlete = combination.Athlete;
        Horse = combination.Horse;
    }
}
