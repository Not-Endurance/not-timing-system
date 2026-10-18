using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// A distinct way an Athlete or a Horse is written (#636): the copies that agree on the legacy id, the name, the name in English and
/// the FEI ID are one spelling, however many documents they are in, so that the matching is done once for them. A Setup row is
/// always a spelling of its own, because the registry is made of its rows.
/// </summary>
public sealed class IdentitySpelling
{
    public IdentitySpelling(string key, IReadOnlyList<IdentityCopy> copies, IReadOnlyDictionary<string, string> events)
    {
        var first = copies[0];
        Key = key;
        Copies = copies;
        IsSetupRow = first.IsSetupRow;
        Observation = new IdentityObservation(
            key,
            first.LegacyId,
            first.Name,
            first.NameEnglish,
            first.FeiId,
            country: null,
            club: null
        );
        Countries = Distinct(copies.Select(x => x.Country));
        Clubs = Distinct(copies.Select(x => x.Club));
        Partners = Distinct(copies.Select(x => x.Partner));
        Events = Distinct(
            copies.Select(x => x.EventId).Select(x => x == null ? null : events.GetValueOrDefault(x, $"event {x}"))
        );
        Rides = copies.Select(x => x.ParticipationId).Where(x => x != null).Distinct().Count();
    }

    /// <summary>What the copies agree on, written as one text: the order the spellings are matched in.</summary>
    public string Key { get; }

    /// <summary>What the matching service is asked about.</summary>
    public IdentityObservation Observation { get; }

    public IReadOnlyList<IdentityCopy> Copies { get; }

    public bool IsSetupRow { get; }

    public string? LegacyId => Copies[0].LegacyId;
    public string? FeiId => Copies[0].FeiId;

    /// <summary>The name as it is stored, and the name in English when it says something else.</summary>
    public string Label
    {
        get
        {
            var first = Copies[0];
            return first.NameEnglish == null
                || string.Equals(first.Name, first.NameEnglish, StringComparison.OrdinalIgnoreCase)
                    ? first.Name ?? first.NameEnglish ?? ""
                : first.Name == null ? first.NameEnglish
                : $"{first.Name} / {first.NameEnglish}";
        }
    }

    // The evidence a review shows. Country and club are not part of what makes a spelling, so a spelling can have several.
    public IReadOnlyList<string> Countries { get; }
    public IReadOnlyList<string> Clubs { get; }

    /// <summary>The Events the copies are in, each with its dates, the earliest first.</summary>
    public IReadOnlyList<string> Events { get; }

    /// <summary>The names of the Horses of an Athlete, or of the Athletes of a Horse, that it was entered with.</summary>
    public IReadOnlyList<string> Partners { get; }

    /// <summary>How many Participations the copies are of: a Participation that a Ranking and a Handout copy as well is one.</summary>
    public int Rides { get; }

    static string[] Distinct(IEnumerable<string?> values)
    {
        return [.. values.Where(x => x != null).Select(x => x!).Distinct().Order(StringComparer.Ordinal)];
    }
}
