using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Services.Identities;

namespace NoTiming.Ui.Features.Console.Registry;

/// <summary>
/// Which rows of the registry the Athlete or Horse being registered is a second copy of, as far as a form can tell (#646, #635,
/// ADR-0010). The rules are the domain's (<see cref="IdentityMatcher"/>), the same that the migration of the registry and the
/// search use: the same FEI ID, the same name in either script by the official transliteration, and the same name by the
/// looser key, which is a spelling of it. What is only a near-miss is not a warning. The form warns and decides nothing.
/// </summary>
public static class SameRows
{
    /// <summary>
    /// The rows of those given that the one being registered is a second copy of, by its FEI ID first and then by its name, the
    /// Tenant's own before the others', with the reason that decided; none is the row itself.
    /// </summary>
    public static IReadOnlyList<SameRow> Among(
        IEnumerable<SameRow> candidates,
        Guid? itself,
        string? name,
        string? nameEnglish,
        string? feiId,
        bool feiIdIsNumber
    )
    {
        var kind = feiIdIsNumber ? IdentityKind.Athlete : IdentityKind.Horse;
        var typed = new IdentityObservation(string.Empty, null, name, nameEnglish, feiId, null, null);
        var rows = candidates
            .Where(x => x.Id != itself)
            .Select(x =>
                (
                    Row: x,
                    Seen: new IdentityObservation(x.Id.ToString(), null, x.Name, x.NameEnglish, x.FeiId, null, null)
                )
            )
            .ToList();
        var rowOf = new Dictionary<string, SameRow>();
        foreach (var (row, seen) in rows)
        {
            rowOf.TryAdd(seen.Key, row);
        }

        var found = new List<SameRow>();
        foreach (var link in IdentityMatcher.Among(kind, typed, rows.Select(x => x.Seen)))
        {
            if (ReasonOf(kind, typed, link) is { } reason)
            {
                found.Add(Of(rowOf[link.Another.Key], reason));
            }
        }

        return [.. found.OrderBy(x => RankOf(x.Reason)).ThenByDescending(x => x.IsOwn)];
    }

    /// <summary>
    /// Why the row is shown: the same FEI ID, or the same name, or a name that is a spelling of it. A FEI ID that the names do
    /// not bear out is still a FEI ID that is taken, and a name that is another's under a FEI ID that differs is still that name.
    /// </summary>
    static SameRowReason? ReasonOf(IdentityKind kind, IdentityObservation typed, IdentityLink link)
    {
        return link.Class switch
        {
            MatchClass.SameFeiId => SameRowReason.FeiId,
            MatchClass.Conflict => IdentityFeiIds.Of(kind, typed.FeiId) == IdentityFeiIds.Of(kind, link.Another.FeiId)
                ? SameRowReason.FeiId
                : SameRowReason.Name,
            MatchClass.ExactName or MatchClass.StrictTransliteration => SameRowReason.Name,
            MatchClass.SpellingVariant => SameRowReason.SimilarName,
            _ => null,
        };
    }

    static int RankOf(SameRowReason reason)
    {
        return reason switch
        {
            SameRowReason.FeiId => 0,
            SameRowReason.Name => 1,
            _ => 2,
        };
    }

    static SameRow Of(SameRow row, SameRowReason reason)
    {
        return new SameRow(row.Id, row.Name, row.NameEnglish, row.FeiId, row.IsOwn, reason);
    }
}
