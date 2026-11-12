using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Application.Contracts.Shared;

namespace NoTiming.Application.Contracts.Core.Models;

public class CombinationModel : PersistedModel
{
    public static CombinationModel MapFrom(Combination combination)
    {
        return new CombinationModel
        {
            DomainVersion = combination.DomainVersion,
            Id = combination.Id,
            Number = combination.Number,
            Distance = combination.Distance,
            Restrictions = SpeedRestrictionsModel.From(combination.Restrictions),
            Athlete = AthleteModel.MapFrom(combination.Athlete),
            Horse = HorseModel.MapFrom(combination.Horse),
        };
    }

    public int Id { get; init; }
    public int Number { get; init; }
    public string? Distance { get; init; }
    public SpeedRestrictionsModel Restrictions { get; init; } = new();
    public AthleteModel Athlete { get; init; } = default!;
    public HorseModel Horse { get; init; } = default!;

    public Combination MapToEntity()
    {
        var athlete = Athlete.MapToEntity();
        var horse = Horse.MapToEntity();
        return new Combination(Number, athlete, horse, athlete.Club, Distance!, Restrictions.ToDomain(), Id, storedDomainVersion: DomainVersion);
    }
}
