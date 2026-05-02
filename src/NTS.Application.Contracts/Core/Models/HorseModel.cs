using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Application.Contracts.Core.Models;

public class HorseModel
{
    public static HorseModel MapFrom(Horse horse)
    {
        return new HorseModel
        {
            Id = horse.Id,
            FeiId = horse.FeiId,
            Name = horse.Name,
        };
    }

    public int Id { get; init; }
    public string? FeiId { get; init; }
    public string Name { get; init; } = default!;

    public Horse MapToEntity()
    {
        return new Horse(Name, FeiId, Id);
    }
}
