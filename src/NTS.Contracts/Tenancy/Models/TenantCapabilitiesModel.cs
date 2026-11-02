namespace NTS.Contracts.Tenancy.Models;

/// <summary>What the Api says a person may do about a Tenant: edit it, make Events in it and write to its registries (ADR-0012).</summary>
public class TenantCapabilitiesModel
{
    public bool CanEditTenant { get; set; }
    public bool CanCreateEvents { get; set; }
    public bool CanEditRegistry { get; set; }
}
