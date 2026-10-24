using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Contracts.Core.Models;

public class ClubModel
{
    public static ClubModel MapFrom(Club club)
    {
        return new ClubModel { Id = club.Id, Name = club.Name };
    }

    public Guid Id { get; init; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; init; } = default!;

    public Club MapToEntity()
    {
        return new Club(Name, Id);
    }
}
