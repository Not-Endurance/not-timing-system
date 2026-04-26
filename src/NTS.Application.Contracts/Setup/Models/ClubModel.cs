using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class ClubModel : IDocument, IKrudModel<Club>
{
    public static ClubModel From(Club club)
    {
        var model = new ClubModel();
        model.MapFrom(club);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; set; } = default!;

    public Club MapToEntity()
    {
        return new Club(Name, Id);
    }

    public void MapFrom(Club club)
    {
        Id = club.Id;
        Name = club.Name;
    }
}
