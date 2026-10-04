using Not.Identity;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The account as the access policy needs to know it when what is asked is about the Tenants and not about one Event: its
/// roles from its document, and the Tenants of the Events it runs that are not yet Historic, which is what lets a Main
/// Operator edit the registry and search the accounts of a Tenant. They are read from the Events, never kept on the
/// account, so a hand-over or the end of an Event is seen at once.
/// </summary>
internal sealed class CallerReader
{
    readonly CrossTenantReads _reads;
    readonly TimeProvider _time;

    public CallerReader(CrossTenantReads reads, TimeProvider time)
    {
        _reads = reads;
        _time = time;
    }

    public async Task<Caller> ReadAsync(NIdentityUser user, CancellationToken cancellationToken)
    {
        var open = await _reads.OpenEventTenantsOfAsync(user.Id, _time.GetUtcNow(), cancellationToken);
        return AccountRoles.CallerOf(user, open);
    }
}
