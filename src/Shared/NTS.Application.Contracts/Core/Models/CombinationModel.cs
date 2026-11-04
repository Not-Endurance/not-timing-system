using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Application.Contracts.Core.Models;

public class CombinationModel
{
    public static CombinationModel MapFrom(Combination combination)
    {
        return new CombinationModel
        {
            Id = combination.Id,
            Number = combination.Number,
            Distance = combination.Distance,
            MinAverageSpeed = combination.MinAverageSpeed,
            MaxAverageSpeed = combination.MaxAverageSpeed,
            Athlete = AthleteModel.MapFrom(combination.Athlete),
            Horse = HorseModel.MapFrom(combination.Horse),
        };
    }

    public int Id { get; init; }
    public int Number { get; init; }
    public string? Distance { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }
    public AthleteModel Athlete { get; init; } = default!;
    public HorseModel Horse { get; init; } = default!;

    public Combination MapToEntity()
    {
        var athlete = Athlete.MapToEntity();
        var horse = Horse.MapToEntity();
        return new Combination(Number, athlete, horse, athlete.Club, Distance!, MinAverageSpeed, MaxAverageSpeed, Id);
    }
}
