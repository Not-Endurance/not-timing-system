using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Contracts.Core.Models;

public class OfficialModel : IDocument, IEventScoped, ISoftDeletableDocument, IKrudModel<Official>
{
    public static OfficialModel MapFrom(Official official)
    {
        var model = new OfficialModel();
        ((IKrudModel<Official>)model).MapFrom(official);
        return model;
    }

    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public Guid EventId { get; set; }
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public OfficialRole Role { get; set; } = default!;
    public Guid? UserId { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Official MapToEntity()
    {
        return new Official(Name, NameEnglish, Role, EventId, Id, UserId);
    }

    void IKrudModel<Official>.MapFrom(Official official)
    {
        Id = official.Id;
        EventId = official.EventId;
        Name = official.Name;
        NameEnglish = official.NameEnglish;
        Role = official.Role;
        UserId = official.UserId;
    }
}
