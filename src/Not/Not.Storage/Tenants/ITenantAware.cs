namespace Not.Storage.Tenants;

public interface ITenantAware
{
    string TenantId { get; }
}
