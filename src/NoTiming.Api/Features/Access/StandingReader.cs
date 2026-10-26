using Not.Identity;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// Where an account stands at a place (ADR-0012): the facts that make its Level, read when they are needed and never
/// kept, so that a grant that is removed or an Event that ends is seen at once. The roles come from the document of the
/// account, the grants of an Event from its grants, and the Tenants of the Events it runs that have not ended from the
/// grants of the account. What an account that is Root at the place has to read is not read: Root outweighs it.
/// </summary>
internal sealed class StandingReader
{
    readonly CrossTenantReads _reads;
    readonly EventGrantStore _grants;
    readonly TimeProvider _time;

    public StandingReader(CrossTenantReads reads, EventGrantStore grants, TimeProvider time)
    {
        _reads = reads;
        _grants = grants;
        _time = time;
    }

    public async Task<Standing> AtEventAsync(NIdentityUser user, EventFacts facts, CancellationToken cancellationToken)
    {
        var roles = AccountRoles.RolesOf(user);
        var held = roles.IsRootAt(facts.TenantId)
            ? []
            : await _grants.GrantsOfAsync(facts.Record, user.Id, cancellationToken);

        return Standing.AtEvent(roles, facts.TenantId, held);
    }

    public async Task<Standing> AtTenantAsync(NIdentityUser user, string tenantId, CancellationToken cancellationToken)
    {
        var roles = AccountRoles.RolesOf(user);
        var runs =
            !roles.IsRootAt(tenantId) && (await OpenEventTenantsAsync(user, cancellationToken)).Contains(tenantId);

        return Standing.AtTenant(roles, tenantId, runs);
    }

    public async Task<Standing> AtPlatformAsync(NIdentityUser user, CancellationToken cancellationToken)
    {
        var roles = AccountRoles.RolesOf(user);
        var runs = !roles.SetsEventsUp && (await OpenEventTenantsAsync(user, cancellationToken)).Count > 0;

        return Standing.AtPlatform(roles, runs);
    }

    /// <summary>The policy's answer for an action of an Event, for the account.</summary>
    public async Task<Verdict> DecideAtEventAsync(
        Capability capability,
        NIdentityUser user,
        EventFacts facts,
        CancellationToken cancellationToken
    )
    {
        var standing = await AtEventAsync(user, facts, cancellationToken);

        return AccessPolicy.Decide(capability, standing, PlaceState.OfEvent(facts.Stage));
    }

    /// <summary>The policy's answer for an action of a Tenant, for the account.</summary>
    public async Task<Verdict> DecideAtTenantAsync(
        Capability capability,
        NIdentityUser user,
        string tenantId,
        bool isOperational,
        CancellationToken cancellationToken
    )
    {
        var standing = await AtTenantAsync(user, tenantId, cancellationToken);

        return AccessPolicy.Decide(capability, standing, PlaceState.OfTenant(isOperational));
    }

    /// <summary>The policy's answer for an action of the platform, for the account.</summary>
    public async Task<Verdict> DecideAtPlatformAsync(
        Capability capability,
        NIdentityUser user,
        CancellationToken cancellationToken
    )
    {
        var standing = await AtPlatformAsync(user, cancellationToken);

        return AccessPolicy.Decide(capability, standing, PlaceState.Platform);
    }

    Task<IReadOnlyList<string>> OpenEventTenantsAsync(NIdentityUser user, CancellationToken cancellationToken)
    {
        return _reads.OpenEventTenantsOfAsync(user.Id, _time.GetUtcNow(), cancellationToken);
    }
}
