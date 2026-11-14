using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Enums;

namespace NoTiming.Application.Contracts.Core.Models;

public class OperatorModel : IEventScoped, ISoftDeletableDocument, IKrudModel<Operator>
{
    public static OperatorModel MapFrom(Operator @operator)
    {
        var model = new OperatorModel();
        model.DomainVersion = @operator.DomainVersion;
        ((IKrudModel<Operator>)model).MapFrom(@operator);
        return model;
    }

    public int DomainVersion { get; set; }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public int UserId { get; set; }
    public OfficialRole Role { get; set; } = OfficialRole.Steward;
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Operator MapToEntity()
    {
        return new Operator(EventId, UserId, Role, Id, storedDomainVersion: DomainVersion);
    }

    void IKrudModel<Operator>.MapFrom(Operator @operator)
    {
        Id = @operator.Id;
        EventId = @operator.EventId;
        UserId = @operator.UserId;
        Role = @operator.Role;
    }
}
