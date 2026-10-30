using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Contracts.Core.Models;

public class OperatorModel : IEventScoped, ISoftDeletableDocument, IKrudModel<Operator>
{
    public static OperatorModel MapFrom(Operator @operator)
    {
        var model = new OperatorModel();
        ((IKrudModel<Operator>)model).MapFrom(@operator);
        return model;
    }

    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public OfficialRole Role { get; set; } = OfficialRole.Steward;
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Operator MapToEntity()
    {
        return new Operator(EventId, UserId, Role, Id);
    }

    void IKrudModel<Operator>.MapFrom(Operator @operator)
    {
        Id = @operator.Id;
        EventId = @operator.EventId;
        UserId = @operator.UserId;
        Role = @operator.Role;
    }
}
