using NTS.Domain.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class EventInformation : Aggregate
{
    public EventInformation(
        Country country,
        string? name,
        string? location,
        EventSpan eventSpan,
        string? feiShowId,
        Guid id,
        bool isActive = true,
        RegionalRules? regionalRules = null,
        string? tenantId = null,
        Guid? mainOperatorId = null
    )
        : base(id)
    {
        Country = Required(nameof(Country), country);
        Name = Required(nameof(Name), name);
        Location = Required(nameof(Location), location);
        EventSpan = eventSpan;
        FeiShowId = feiShowId;
        IsActive = isActive;
        RegionalRules = regionalRules ?? RegionalRules.None;
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? Tenant.LEGACY_ID : tenantId;
        MainOperatorId = mainOperatorId;
    }

    public Country Country { get; }
    public string Name { get; }
    public string Location { get; }
    public EventSpan EventSpan { get; }
    public string? FeiShowId { get; }
    public bool IsActive { get; }

    /// <summary>The Tenant the Event belongs to (ADR-0012). An Event from before Tenants has the constant one.</summary>
    public string TenantId { get; }

    /// <summary>
    /// The one account that runs the Event, as it was when the Event started or as a hand-over left it; none for an
    /// Event from before Tenants.
    /// </summary>
    public Guid? MainOperatorId { get; }

    /// <summary>
    /// The rules of the Regional competitions as the Tenant had them when the Event started (ADR-0012). The Phases and
    /// the Results of the Event are judged by these, and a later change of the Tenant's rules does not reach them.
    /// </summary>
    public RegionalRules RegionalRules { get; }

    /// <summary>Whether the Event is Live at the instant: a rule of its span and a clock, and never a stored flag (ADR-0007).</summary>
    public bool IsLive(DateTimeOffset now)
    {
        return EventSpan.IsLive(now);
    }

    public override string ToString()
    {
        return $"{Name} {Location} {Country} {EventSpan}";
    }
}
