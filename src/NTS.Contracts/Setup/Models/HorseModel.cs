using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Contracts.Setup.Models;

public class HorseModel : IDocument, IKrudModel<Horse>
{
    public static HorseModel From(Horse horse)
    {
        var model = new HorseModel();
        model.MapFrom(horse);
        return model;
    }

    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public string? FeiId { get; set; }

    public void MapFrom(Horse horse)
    {
        Id = horse.Id;
        Name = horse.Name;
        NameEnglish = horse.NameEnglish;
        FeiId = horse.FeiId;
    }

    public Horse MapToEntity()
    {
        return new Horse(Name, NameEnglish, FeiId, Id);
    }
}
