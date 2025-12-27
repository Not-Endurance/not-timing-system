using Not.Domain;
using Not.Storage.Tenants;
using Not.Structures;

namespace NTS.Application.DataTransferObjects;

public abstract class Identity : IIdentifiable, ITenantAware, IAggregateRoot
{
    public int Id { get; init; }
    public string TenantId { get; set; } = "nts";
}
