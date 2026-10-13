using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// Who may read how the times of an Event were recorded (#619, ADR-0005, ADR-0012): Staff, which is every Official and every
/// Operator that was linked to the Event, its Main Operator, and the Developer. They are the people the policy's
/// <see cref="Capability.SeeTimeEvents"/> names, and it is asked here and nowhere else, so what a read shows and what the
/// capabilities say cannot differ. Anybody else, signed in or not, reads the times each Phase shows and nothing of how they
/// were made. A grant that is still pending, and one that is of another Event, makes nobody Staff.
/// </summary>
internal sealed class TimeEventViewers
{
    readonly EventGrantStore _grants;

    public TimeEventViewers(EventGrantStore grants)
    {
        _grants = grants;
    }

    /// <summary>The policy's answer for a person who is signed in, with what they have been granted on the Event.</summary>
    public async Task<Verdict> DecideAsync(NIdentityUser user, EventFacts facts, CancellationToken cancellationToken)
    {
        var grants = await _grants.GrantsOfAsync(facts.Record, user.Id, cancellationToken);
        return AccessPolicy.Decide(Capability.SeeTimeEvents, AccountRoles.CallerOf(user), facts.ScopeFor(grants));
    }

    /// <summary>Whether the caller of the request may see how the times of the Event were recorded; a caller who is not signed in may not.</summary>
    public async Task<bool> MaySeeAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventFacts facts,
        CancellationToken cancellationToken
    )
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var user = await users.GetUserAsync(context.User);
        return user is not null && (await DecideAsync(user, facts, cancellationToken)).IsAllowed;
    }
}
