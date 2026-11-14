using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Enums;

namespace NoTiming.Application.Contracts.Core.Models;

public class OfficialModel : IEventScoped, ISoftDeletableDocument, IKrudModel<Official>
{
    public static OfficialModel MapFrom(Official official)
    {
        var model = new OfficialModel();
        ((IKrudModel<Official>)model).MapFrom(official);
        return model;
    }

    public int DomainVersion { get; set; }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public OfficialRole Role { get; set; } = default!;
    public int? UserId { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Official MapToEntity()
    {
        return new Official(Name, NameEnglish, Role, EventId, Id, UserId, storedDomainVersion: DomainVersion);
    }

    void IKrudModel<Official>.MapFrom(Official official)
    {
        DomainVersion = official.DomainVersion;
        Id = official.Id;
        EventId = official.EventId;
        Name = official.Name;
        NameEnglish = official.NameEnglish;
        Role = official.Role;
        UserId = official.UserId;
    }
}
