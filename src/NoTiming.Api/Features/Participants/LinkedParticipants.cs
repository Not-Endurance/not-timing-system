using System.Globalization;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;

namespace NoTiming.Api.Features.Participants;

/// <summary>The Athlete and the Horses a person is linked to, by name, as their profile tells them (#638).</summary>
internal sealed class LinkedNames
{
    public LinkedNames(ParticipantName? athlete, IReadOnlyList<ParticipantName> horses)
    {
        Athlete = athlete;
        Horses = horses;
    }

    public ParticipantName? Athlete { get; }

    /// <summary>The Horses in the order the person added them.</summary>
    public IReadOnlyList<ParticipantName> Horses { get; }
}

/// <summary>
/// Reads the links of a person together with the names of what they point at, which is what the profile of the person shows
/// (#638, ADR-0010). A link to a row that has been deleted since is not shown: it has no name to show, and the person can still
/// take it away.
/// </summary>
internal sealed class LinkedParticipants
{
    readonly ParticipantLinkStore _store;
    readonly CrossTenantReads _reads;

    public LinkedParticipants(ParticipantLinkStore store, CrossTenantReads reads)
    {
        _store = store;
        _reads = reads;
    }

    public async Task<LinkedNames> ReadAsync(
        NIdentityUser user,
        CultureInfo culture,
        CancellationToken cancellationToken
    )
    {
        var links = await _store.FindAsync(user.Id, cancellationToken);
        var caller = AccountRoles.CallerOf(user);
        var athlete = links.AthleteId is { } id
            ? (
                await _reads.FindParticipantsAsync(caller, ParticipantKind.Athlete, [id], cancellationToken)
            ).FirstOrDefault()
            : null;
        var horses = (
            await _reads.FindParticipantsAsync(caller, ParticipantKind.Horse, links.HorseIds, cancellationToken)
        ).ToDictionary(x => x.Id);
        return new LinkedNames(
            athlete?.NameIn(null, culture),
            [.. links.HorseIds.Where(horses.ContainsKey).Select(x => horses[x].NameIn(null, culture))]
        );
    }
}
