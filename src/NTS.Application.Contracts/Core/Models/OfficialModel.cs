using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Application.Contracts.Core.Models;

public class OfficialModel : IEventScopedDocument, ISoftDeletableDocument, IKrudModel<Official>
{
    public static OfficialModel MapFrom(Official official)
    {
        var model = new OfficialModel();
        ((IKrudModel<Official>)model).MapFrom(official);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public string[] Names { get; set; } = [];
    public OfficialRole Role { get; set; } = default!;
    public int? UserId { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Official MapToEntity()
    {
        return new Official(Names, Role, EventId, Id, UserId);
    }

    void IKrudModel<Official>.MapFrom(Official official)
    {
        Id = official.Id;
        EventId = official.EventId;
        Names = official.Person.Names;
        Role = official.Role;
        UserId = official.UserId;
    }
}
