using NTS.Domain.Aggregates;
using NTS.Domain.Objects;

namespace NTS.Contracts.Tenancy.Models;

/// <summary>
/// A Tenant as the Api serves it at <c>/api/tenants/{id}</c>: its key, its name, its kind and the rules its Regional
/// competitions use (ADR-0012). Its key is text, not a Guid (ADR-0002), so a Tenant is not an entity of the repositories
/// that keep Guids; it is read and changed at its path.
/// </summary>
public class TenantModel
{
    public static TenantModel MapFrom(Tenant tenant)
    {
        return new TenantModel
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Kind = tenant.Kind,
            RegionalRules = new RegionalRulesModel
            {
                OnlyAverageLoopSpeed = tenant.RegionalRules.OnlyAverageLoopSpeed,
                RankerCode = tenant.RegionalRules.RankerCode,
            },
        };
    }

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public RegionalRulesModel RegionalRules { get; set; } = new();

    public Tenant MapToEntity()
    {
        return new Tenant(
            Id,
            Name,
            Kind,
            new RegionalRules(RegionalRules.OnlyAverageLoopSpeed, RegionalRules.RankerCode)
        );
    }
}

/// <summary>The rules of a Tenant's Regional competitions: whether only the average loop speed counts, and the ranker that ranks them.</summary>
public class RegionalRulesModel
{
    public bool OnlyAverageLoopSpeed { get; set; }
    public string? RankerCode { get; set; }
}
