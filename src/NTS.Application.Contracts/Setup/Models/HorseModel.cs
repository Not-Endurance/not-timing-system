using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;


namespace NTS.Application.Contracts.Setup.Models;

public class HorseModel : IDocument, IKrudModel<Horse>
{
    public static HorseModel From(Horse horse)
    {
        var model = new HorseModel();
        model.MapFrom(horse);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; set; } = default!;
    public string? FeiId { get; set; }

    public void MapFrom(Horse horse)
    {
        Id = horse.Id;
        Name = horse.Name;
        FeiId = horse.FeiId;
    }

    public Horse MapToEntity()
    {
        return new Horse(Name, FeiId, Id);
    }
}


