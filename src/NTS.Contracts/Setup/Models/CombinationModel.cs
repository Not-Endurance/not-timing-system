using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Setup.Models;

public class CombinationModel
{
    public static CombinationModel MapFrom(Combination combination)
    {
        return new CombinationModel
        {
            Id = combination.Id,
            Number = combination.Number,
            Athlete = AthleteModel.From(combination.Athlete),
            Horse = HorseModel.From(combination.Horse),
        };
    }

    public Guid Id { get; init; }
    public int Number { get; init; }
    public AthleteModel Athlete { get; init; } = default!;
    public HorseModel Horse { get; init; } = default!;

    public Combination MapToEntity()
    {
        var athlete = Athlete.MapToEntity();
        var horse = Horse.MapToEntity();
        return new Combination(Number, athlete, horse, Id);
    }
}
