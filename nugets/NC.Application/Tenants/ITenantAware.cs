namespace Not.Application.Tenants;

public interface ITenantAware
{
    string TenantId { get; }
}
