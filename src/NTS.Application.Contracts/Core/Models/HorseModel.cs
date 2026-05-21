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
            NameEnglish = horse.NameEnglish,
        };
    }

    public int Id { get; init; }
    public string? FeiId { get; init; }
    public string Name { get; init; } = default!;
    public string? NameEnglish { get; init; }

    public Horse MapToEntity()
    {
        return new Horse(Name, NameEnglish, FeiId, Id);
    }
}
