namespace NC.Application.Tenants;

public interface ITenantAware
{
    string TenantId { get; }
}
