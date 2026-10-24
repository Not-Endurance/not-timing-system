using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// One Athlete or one Horse as the registry will have it (#637, ADR-0010): the Guid every copy of it ends with, and what becomes of the
/// Setup rows it already is, or of the row it will be given.
/// </summary>
public sealed class AssignedIdentity
{
    public AssignedIdentity(
        IdentityKind kind,
        Guid id,
        IReadOnlyList<string> spellings,
        string? survivor,
        IReadOnlyList<string> merged,
        BsonDocument? create,
        string? nameEnglish
    )
    {
        Kind = kind;
        Id = id;
        Spellings = spellings;
        Survivor = survivor;
        Merged = merged;
        Create = create;
        NameEnglish = nameEnglish;
    }

    public IdentityKind Kind { get; }
    public Guid Id { get; }

    /// <summary>The spellings that are this identity, by their keys.</summary>
    public IReadOnlyList<string> Spellings { get; }

    /// <summary>The id of the Setup row that stays and takes <see cref="Id"/>, as text; none when a row is to be made.</summary>
    public string? Survivor { get; }

    /// <summary>The ids of the other Setup rows, which are deleted once their copies are re-pointed.</summary>
    public IReadOnlyList<string> Merged { get; }

    /// <summary>The row to make, without its id, built from the latest copy; none when the identity is a row already.</summary>
    public BsonDocument? Create { get; }

    /// <summary>What the English name of the surviving row is filled with, when it is empty and a copy has the Latin spelling.</summary>
    public string? NameEnglish { get; }
}

/// <summary>
/// Which Guid every Athlete and every Horse ends with, and what becomes of the Setup rows (#637, ADR-0009, ADR-0010). The Guid of an
/// identity is the one that is written already in its rows or its copies; else the one of its surviving Setup row (the row most
/// Participations ride on, then the lowest id), else of its lowest legacy id that is not 0, else of its FEI ID or its smallest name.
/// It is made once, from the data as it was before the first write, and kept (<see cref="ToDocument"/>), because a run that stopped has
/// converted some copies and so no longer has the evidence the matching needs. It refuses what the data and the decisions do not agree on.
/// </summary>
public sealed class IdentityAssignments : IIdentityGuids
{
    const string IDENTITIES = "Identities";

    /// <param name="plan">The plan of the dry run, over the data as it is.</param>
    /// <param name="eventDates">When each Event began (when its first Competition starts, for a Setup), by the id the copies name it by.</param>
    /// <param name="countries">The documents of the countries, which tell the ISO country of a row that is made.</param>
    public static IdentityAssignments Build(
        IdentityPlan plan,
        IReadOnlyDictionary<string, DateTime> eventDates,
        IReadOnlyList<BsonDocument> countries
    )
    {
        var refusals = new List<string>();
        var identities = new List<AssignedIdentity>();

        // What the decisions and the names settle matters while there is something to convert. Once every copy has its Guid they settle
        // nothing, and the data has changed since they were taken, by the run that converted it: a second run is no mistake.
        var hasWork = plan.Kinds.Any(x => x.Copies.Any(y => y.CurrentId == null));
        foreach (var kind in plan.Kinds)
        {
            if (hasWork)
            {
                RefuseWhatIsNotSettled(kind, refusals);
            }

            identities.AddRange(new KindAssignment(kind, eventDates, countries, refusals).Assign());
        }

        if (hasWork)
        {
            refusals.AddRange(
                plan.UnusedDecisions.Select(x =>
                    $"The decision for '{x}' matches nothing in the data: a typing mistake, or data that has changed."
                )
            );
        }
        return new IdentityAssignments(identities, refusals, "");
    }

    public static IdentityAssignments FromDocument(BsonDocument document)
    {
        var identities = document[IDENTITIES]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .Select(x => new AssignedIdentity(
                Enum.Parse<IdentityKind>(x["Kind"].AsString),
                x["Id"].AsGuid,
                [.. x["Spellings"].AsBsonArray.Select(y => y.AsString)],
                x.TryGetValue("Survivor", out var survivor) ? survivor.AsString : null,
                [.. x["Merged"].AsBsonArray.Select(y => y.AsString)],
                x.TryGetValue("Create", out var create) ? create.AsBsonDocument : null,
                x.TryGetValue("NameEnglish", out var english) ? english.AsString : null
            ))
            .ToList();
        return new IdentityAssignments(identities, [], document["Decisions"].AsString);
    }

    readonly Dictionary<(IdentityKind Kind, string Key), Guid> _byKey = [];

    IdentityAssignments(IReadOnlyList<AssignedIdentity> identities, IReadOnlyList<string> refusals, string decisions)
    {
        Identities = identities;
        Refusals = refusals;
        Decisions = decisions;
        foreach (var identity in identities)
        {
            foreach (var key in identity.Spellings)
            {
                _byKey[(identity.Kind, IdentityKeys.Told(key))] = identity.Id;
            }
        }
    }

    public IReadOnlyList<AssignedIdentity> Identities { get; }

    /// <summary>What stops the run, each as a sentence: nothing is written while there is one.</summary>
    public IReadOnlyList<string> Refusals { get; }

    /// <summary>A fingerprint of the decisions the assignments were made with; a run that is resumed must have the same.</summary>
    public string Decisions { get; }

    /// <summary>The assignments as the document they are kept in while the run lasts.</summary>
    /// <param name="decisions">A fingerprint of the decisions they were made with.</param>
    public BsonDocument ToDocument(string decisions)
    {
        return new BsonDocument
        {
            { "Decisions", decisions },
            {
                IDENTITIES,
                new BsonArray(
                    Identities.Select(x =>
                    {
                        var identity = new BsonDocument
                        {
                            { "Kind", x.Kind.ToString() },
                            { "Id", new BsonBinaryData(x.Id, GuidRepresentation.Standard) },
                            { "Spellings", new BsonArray(x.Spellings) },
                            { "Merged", new BsonArray(x.Merged) },
                        };
                        if (x.Survivor != null)
                        {
                            identity["Survivor"] = x.Survivor;
                        }

                        if (x.Create != null)
                        {
                            identity["Create"] = x.Create;
                        }

                        if (x.NameEnglish != null)
                        {
                            identity["NameEnglish"] = x.NameEnglish;
                        }

                        return identity;
                    })
                )
            },
        };
    }

    public Guid? GuidOf(IdentityKind kind, string spellingKey)
    {
        return _byKey.TryGetValue((kind, IdentityKeys.Told(spellingKey)), out var id) ? id : null;
    }

    static void RefuseWhatIsNotSettled(IdentityKindPlan kind, List<string> refusals)
    {
        if (kind.Unreadable.Count > 0)
        {
            var first = kind.Unreadable[0];
            refusals.Add(
                $"{kind.Unreadable.Count} {kind.Kind} copies have no name, so that nothing can be said of them; the first is in "
                    + $"{first.Collection} {first.DocumentId} {first.Path}. Give them a name or remove them, and run it again."
            );
        }

        refusals.AddRange(
            kind.Contradictions.Select(x =>
                $"The decision of 'different' for '{x.Key}' is overruled by other decisions of 'same' that join the two: resolve them."
            )
        );
        refusals.AddRange(
            kind.Entries.Where(x => x.Decision == IdentityDecision.Same && !x.IsApplied)
                .Select(x =>
                    $"The decision of 'same' for '{x.Key}' waits for a group that is held and not decided to be one: decide that group first."
                )
        );
    }

    /// <summary>The assignments of the Athletes, or of the Horses.</summary>
    sealed class KindAssignment
    {
        readonly IdentityKindPlan _plan;
        readonly IReadOnlyDictionary<string, DateTime> _dates;
        readonly IReadOnlyList<BsonDocument> _countries;
        readonly List<string> _refusals;
        readonly string _space;
        readonly HashSet<Guid> _taken = [];

        public KindAssignment(
            IdentityKindPlan plan,
            IReadOnlyDictionary<string, DateTime> dates,
            IReadOnlyList<BsonDocument> countries,
            List<string> refusals
        )
        {
            _plan = plan;
            _dates = dates;
            _countries = countries;
            _refusals = refusals;
            _space = plan.Kind == IdentityKind.Athlete ? IdPaths.ATHLETE : IdPaths.HORSE;
        }

        public IEnumerable<AssignedIdentity> Assign()
        {
            var units = Units();
            foreach (var (_, guid) in units)
            {
                if (guid != null)
                {
                    _taken.Add(guid.Value);
                }
            }

            return
            [
                .. units.OrderBy(x => x.Identity.SetupRows == 0 ? 1 : 0).Select(x => AssignOne(x.Identity, x.Guid)),
            ];
        }

        /// <summary>
        /// The identities, with the ones that carry the same Guid made one (a Guid that is written says they are, whatever the evidence
        /// that joined them has become), and the Guid each has already, none for an identity with none. An identity that has two Guids,
        /// or that an item decision of <c>different</c> keeps apart from the one it shares its Guid with, is refused and left out.
        /// </summary>
        List<(PlannedIdentity Identity, Guid? Guid)> Units()
        {
            var identities = _plan.Identities;
            var guids = identities
                .Select(x => Copies(x).Select(y => y.CurrentId).OfType<Guid>().Distinct().Order().ToList())
                .ToList();
            var parent = Enumerable.Range(0, identities.Count).ToArray();
            var first = new Dictionary<Guid, int>();
            for (var i = 0; i < identities.Count; i++)
            {
                foreach (var guid in guids[i])
                {
                    if (first.TryGetValue(guid, out var other))
                    {
                        parent[Root(parent, i)] = Root(parent, other);
                    }
                    else
                    {
                        first[guid] = i;
                    }
                }
            }

            var indexOf = new Dictionary<IdentitySpelling, int>();
            for (var i = 0; i < identities.Count; i++)
            {
                foreach (var spelling in identities[i].Spellings)
                {
                    indexOf[spelling] = i;
                }
            }

            var units = new List<(PlannedIdentity, Guid?)>();
            foreach (
                var members in Enumerable.Range(0, identities.Count).GroupBy(x => Root(parent, x)).OrderBy(x => x.Min())
            )
            {
                var indexes = members.ToList();
                var written = indexes.SelectMany(x => guids[x]).Distinct().Order().ToList();
                var label = Label(identities[indexes[0]]);
                if (
                    written.Count > 1
                    && !IsDecided(indexes, identities)
                    && indexes.All(x => IsConverted(identities[x]))
                )
                {
                    // an earlier run kept them apart, as it had to: the evidence that joins them now is that the ids that told them
                    // apart are Guids, and the Guids are the evidence. There is nothing to assign to what is written already.
                    continue;
                }

                if (written.Count > 1)
                {
                    _refusals.Add(
                        $"{label} has two Guids already written ({string.Join(", ", written)}): a decision contradicts "
                            + "what an earlier run wrote, or the data was changed since."
                    );
                    continue;
                }

                if (indexes.Count > 1 && Contradiction(indexes, indexOf) is { } entry)
                {
                    _refusals.Add(
                        $"The decision of 'different' for '{entry.Key}' contradicts the Guid {written[0]}, which an earlier run "
                            + "wrote for both: they are one Athlete or Horse now."
                    );
                    continue;
                }

                var one =
                    indexes.Count == 1
                        ? identities[indexes[0]]
                        : new PlannedIdentity(
                            [
                                .. indexes
                                    .SelectMany(x => identities[x].Spellings)
                                    .OrderBy(x => x.Key, StringComparer.Ordinal),
                            ],
                            byDecision: false
                        );
                units.Add((one, written.Count == 1 ? written[0] : null));
            }

            return units;
        }

        /// <summary>Whether a decision of the owner joined any of the identities, as against the rules alone.</summary>
        static bool IsDecided(List<int> indexes, IReadOnlyList<PlannedIdentity> identities)
        {
            return indexes.Any(x => identities[x].ByDecision);
        }

        /// <summary>Whether every copy of the identity has its Guid already.</summary>
        static bool IsConverted(PlannedIdentity identity)
        {
            return Copies(identity).All(x => x.CurrentId != null);
        }

        /// <summary>An item decision of <c>different</c> (a rule of a class is not one) between spellings that the identities joined by a Guid hold.</summary>
        IdentityEntry? Contradiction(List<int> indexes, Dictionary<IdentitySpelling, int> indexOf)
        {
            foreach (
                var entry in _plan.Entries.Where(x => x is { Decision: IdentityDecision.Different, ByRule: false })
            )
            {
                var sides = entry
                    .Left.Spellings.Concat(entry.Right?.Spellings ?? [])
                    .Select(x => indexOf[x])
                    .Distinct()
                    .ToList();
                if (sides.Count > 1 && sides.All(indexes.Contains))
                {
                    return entry;
                }
            }

            return null;
        }

        static int Root(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        AssignedIdentity AssignOne(PlannedIdentity identity, Guid? written)
        {
            var copies = Copies(identity);
            var rows = copies
                .Where(x => x.IsSetupRow)
                .OrderByDescending(x => Rides(x, copies))
                .ThenBy(Number)
                .ThenBy(x => x.DocumentId, StringComparer.Ordinal)
                .ToList();
            var survivor = rows.FirstOrDefault(x => written != null && x.CurrentId == written) ?? rows.FirstOrDefault();
            var id = written ?? Candidates(identity, rows, copies).First(x => !_taken.Contains(x));
            _taken.Add(id);
            return new AssignedIdentity(
                _plan.Kind,
                id,
                [.. identity.Spellings.Select(x => x.Key)],
                survivor?.DocumentId,
                [.. rows.Where(x => x != survivor).Select(x => x.DocumentId)],
                survivor == null ? RowOf(copies) : null,
                survivor is { NameEnglish: null or "", Name: { } name } && IsCyrillic(name)
                    ? LatinSpelling(Latest(copies), name)
                    : null
            );
        }

        /// <summary>Where the Guid could come from, the best first: a Guid is taken from the next when another identity has it.</summary>
        IEnumerable<Guid> Candidates(PlannedIdentity identity, List<IdentityCopy> rows, List<IdentityCopy> copies)
        {
            foreach (var row in rows)
            {
                if (long.TryParse(row.LegacyId, NumberStyles.None, CultureInfo.InvariantCulture, out var legacy))
                {
                    yield return LegacyGuids.Of(_space, null, legacy);
                }
            }

            foreach (var legacy in copies.Select(Number).Where(x => x != long.MaxValue).Distinct().Order())
            {
                yield return LegacyGuids.Of(_space, null, legacy);
            }

            var fei = copies
                .Select(x => IdentityFeiIds.Of(_plan.Kind, x.FeiId))
                .Where(x => !string.IsNullOrEmpty(x))
                .Order(StringComparer.Ordinal)
                .FirstOrDefault();
            if (fei != null)
            {
                yield return LegacyGuids.Of(_space, null, $"fei:{fei}");
            }

            var name = copies
                .SelectMany(x => new[] { x.Name, x.NameEnglish })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => IdentityNames.Normalised(x!).ToLowerInvariant())
                .Order(StringComparer.Ordinal)
                .FirstOrDefault();
            if (name != null)
            {
                yield return LegacyGuids.Of(_space, null, $"name:{name}");
            }

            yield return LegacyGuids.Of(_space, null, $"identity:{identity.Spellings[0].Key}");
        }

        /// <summary>The Participations that ride on a row: the ones the copies that carry its id are of.</summary>
        static int Rides(IdentityCopy row, List<IdentityCopy> copies)
        {
            return row.LegacyId == null
                ? 0
                : copies
                    .Where(x => x.LegacyId == row.LegacyId && x.ParticipationId != null)
                    .Select(x => x.ParticipationId)
                    .Distinct()
                    .Count();
        }

        static long Number(IdentityCopy copy)
        {
            return long.TryParse(copy.LegacyId, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                ? number
                : long.MaxValue;
        }

        static List<IdentityCopy> Copies(PlannedIdentity identity)
        {
            return [.. identity.Spellings.SelectMany(x => x.Copies)];
        }

        static string Label(PlannedIdentity identity)
        {
            return identity.Spellings[0].Label;
        }

        /// <summary>The copies, the latest Event's first. A Setup row is older than any Event, and copies of one Event are told by their place.</summary>
        List<IdentityCopy> Latest(IEnumerable<IdentityCopy> copies)
        {
            return
            [
                .. copies
                    .OrderByDescending(x =>
                        x.EventId != null && _dates.TryGetValue(x.EventId, out var date) ? date : DateTime.MinValue
                    )
                    .ThenByDescending(x => x.Collection, StringComparer.Ordinal)
                    .ThenByDescending(x => x.DocumentId, StringComparer.Ordinal)
                    .ThenByDescending(x => x.Path, StringComparer.Ordinal),
            ];
        }

        /// <summary>
        /// The row an identity with none is given, built from its latest copy: the Name is the Cyrillic spelling if there is one and the
        /// Latin one otherwise, the NameEnglish the Latin spelling when it differs, the Country the ISO country, the Club the copy's.
        /// </summary>
        BsonDocument RowOf(List<IdentityCopy> copies)
        {
            var latest = Latest(copies);
            var name =
                latest.Select(x => x.Name).FirstOrDefault(x => x != null && IsCyrillic(x))
                ?? latest.Select(x => x.Name).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? latest.Select(x => x.NameEnglish).First(x => !string.IsNullOrWhiteSpace(x))!;
            var row = new BsonDocument
            {
                { "TenantId", latest.Select(x => x.TenantId).FirstOrDefault(x => x != null) ?? "nts" },
            };
            row["Name"] = name;
            if (IsCyrillic(name) && LatinSpelling(latest, name) is { } english)
            {
                row["NameEnglish"] = english;
            }

            if (latest.Select(x => x.FeiId).FirstOrDefault(x => x != null) is { } fei)
            {
                row["FeiId"] = fei;
            }

            if (_plan.Kind == IdentityKind.Athlete)
            {
                if (latest.Select(x => x.CountryDocument).FirstOrDefault(x => x != null) is { } country)
                {
                    row["Country"] = IsoCountry(country);
                }

                if (latest.Select(x => x.ClubDocument).FirstOrDefault(x => x != null) is { } club)
                {
                    row["Club"] = club.DeepClone();
                }
            }

            return row;
        }

        /// <summary>The Latin spelling that is not the name given: an English name first, and else a name written in Latin letters.</summary>
        static string? LatinSpelling(List<IdentityCopy> latestFirst, string? differsFrom)
        {
            return latestFirst
                    .Select(x => x.NameEnglish)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && !SameText(x, differsFrom))
                ?? latestFirst
                    .Select(x => x.Name)
                    .FirstOrDefault(x => x != null && IsLatin(x) && !SameText(x, differsFrom));
        }

        /// <summary>
        /// The row of the countries that the country of a copy is: by its id, else by its ISO code, its code of the federation or its
        /// name. Bulgaria is entered as BGR and as BUL, and the row it is is the ISO country; a country that no row is stays as it is.
        /// </summary>
        BsonValue IsoCountry(BsonDocument country)
        {
            var id = country.TryGetValue("_id", out var value) ? IdentityCopies.Describe(value) : "0";
            var row =
                (
                    id == "0"
                        ? null
                        : _countries.FirstOrDefault(x => x.Contains("_id") && IdentityCopies.Describe(x["_id"]) == id)
                )
                ?? _countries.FirstOrDefault(x => Same(x, country, "IsoCode"))
                ?? _countries.FirstOrDefault(x => Same(x, country, "NfCode"))
                ?? _countries.FirstOrDefault(x => Same(x, country, "Name"));
            return (row ?? country).DeepClone();
        }

        static bool Same(BsonDocument row, BsonDocument country, string field)
        {
            return row.TryGetValue(field, out var one)
                && country.TryGetValue(field, out var another)
                && one.IsString
                && another.IsString
                && SameText(one.AsString, another.AsString);
        }

        static bool SameText(string? one, string? another)
        {
            return string.Equals(one?.Trim(), another?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static bool IsCyrillic(string text)
        {
            return Regex.IsMatch(text, @"\p{IsCyrillic}");
        }

        static bool IsLatin(string text)
        {
            return !IsCyrillic(text) && text.Any(x => x is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'));
        }
    }
}
