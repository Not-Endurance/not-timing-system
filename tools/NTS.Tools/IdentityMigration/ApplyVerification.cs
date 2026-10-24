using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.Shared;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// What a run must not change, taken from the data before it (#637): how many Events, Participations, Rankings and Handouts there are,
/// and what every Core document says in its names. The documents are told by the Guid their id becomes, so that the same document is
/// found before and after.
/// </summary>
internal sealed class DataFacts
{
    static readonly string[] COUNTED =
    [
        IdPaths.EVENT_INFORMATIONS,
        IdPaths.PARTICIPATIONS,
        "event_rankings",
        "event_handouts",
    ];
    static readonly string[] NAMED =
    [
        IdPaths.PARTICIPATIONS,
        "event_officials",
        IdPaths.EVENT_INFORMATIONS,
        "event_rankings",
    ];

    /// <summary>Reads the facts of the data as it is stored.</summary>
    public static async Task<DataFacts> ReadAsync(IMongoDatabase database, int pageSize, Func<TimeSpan, Task>? wait)
    {
        var facts = new DataFacts();
        foreach (var collection in COUNTED.Union(NAMED))
        {
            await foreach (
                var document in ThrottledReads.Documents(
                    database.GetCollection<BsonDocument>(collection),
                    pageSize,
                    wait
                )
            )
            {
                facts.Add(collection, document, IdConversion.Convert(collection, document, null).Id);
            }
        }

        return facts;
    }

    readonly Dictionary<string, HashSet<Guid>> _identified = [];
    readonly Dictionary<string, long> _unidentified = [];

    /// <summary>
    /// How many documents each counted collection has. A document is told by the Guid its id becomes, so that an original and its twin,
    /// which a run that stopped between the two writes leaves, are one document.
    /// </summary>
    public IReadOnlyDictionary<string, long> Counts =>
        _identified
            .Keys.Union(_unidentified.Keys)
            .ToDictionary(
                x => x,
                x => (_identified.TryGetValue(x, out var ids) ? ids.Count : 0) + _unidentified.GetValueOrDefault(x)
            );

    public Dictionary<(string Collection, Guid Id), string> Names { get; } = [];

    /// <param name="id">The Guid the document's id is or becomes, none for a document whose id is not one.</param>
    public void Add(string collection, BsonDocument document, Guid? id)
    {
        var isCounted = COUNTED.Contains(collection);
        var isNamed = NAMED.Contains(collection);
        if (!isCounted && !isNamed)
        {
            return;
        }

        if (isCounted && id is { } identified)
        {
            if (!_identified.TryGetValue(collection, out var ids))
            {
                _identified[collection] = ids = [];
            }

            ids.Add(identified);
        }
        else if (isCounted)
        {
            _unidentified[collection] = _unidentified.GetValueOrDefault(collection) + 1;
        }

        if (isNamed && id is { } named)
        {
            var lines = new List<string>();
            NamesOf(document, "", lines);
            lines.Sort(StringComparer.Ordinal);
            Names[(collection, named)] = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))
            );
        }
    }

    static void NamesOf(BsonValue value, string path, List<string> lines)
    {
        switch (value)
        {
            case BsonDocument document:
                foreach (var element in document.Elements)
                {
                    var child = path.Length == 0 ? element.Name : $"{path}.{element.Name}";
                    if (element.Name is "Name" or "NameEnglish" && element.Value.IsString)
                    {
                        lines.Add($"{child}={element.Value.AsString}");
                    }
                    else
                    {
                        NamesOf(element.Value, child, lines);
                    }
                }

                break;
            case BsonArray array:
                foreach (var item in array)
                {
                    NamesOf(item, path, lines);
                }

                break;
        }
    }
}

/// <summary>
/// The read-only pass that follows an apply, and runs over the planned result in a dry run (#637): no integer id or reference
/// remains, every reference resolves, every Athlete and Horse id is a Setup row's, every Ranking of an Event that has ended has its
/// placings, the counts of Events, Participations, Rankings and Handouts are unchanged, and the names in Core are. It tells pass or
/// fail for each, and what does not hold.
/// </summary>
internal static class ApplyVerification
{
    const int LISTED = 8;

    /// <param name="database">The database, for the collections that the table does not know.</param>
    /// <param name="read">The documents of a collection after the run, or as they would be.</param>
    /// <param name="before">What the data said before.</param>
    /// <param name="now">The instant an Event is told Historic at.</param>
    /// <param name="finalised">What the finalisation did; none for a dry run, which only says what an apply will finalise.</param>
    public static async Task<List<VerificationCheck>> RunAsync(
        IMongoDatabase database,
        Func<string, IAsyncEnumerable<BsonDocument>> read,
        DataFacts before,
        DateTimeOffset now,
        RankingsReport? finalised
    )
    {
        var after = new DataFacts();
        var integers = new List<string>();
        var ids = IdPaths.Collections.ToDictionary(x => x, _ => new HashSet<Guid>());
        var references = new List<(string Source, string Document, IdReference Reference)>();
        var people = new List<(string Source, string Document, IdPerson Person)>();
        var historic = new HashSet<Guid>();
        var rankings = new List<(Guid Id, Guid Event, PlacingsState State)>();
        foreach (var collection in IdPaths.Collections)
        {
            await foreach (var document in read(collection))
            {
                var converted = IdConversion.Convert(collection, document, null);
                var documentId = document.TryGetValue("_id", out var value) ? IdentityCopies.Describe(value) : "";
                integers.AddRange(
                    converted.Problems.Select(x => $"{x.Collection} {x.DocumentId} {x.Path}: {x.Message}")
                );
                if (converted.Changed && converted.Problems.Count == 0)
                {
                    integers.Add($"{collection} {documentId}: an integer id or reference");
                }

                if (Records.GuidOf(document, "_id") is { } id)
                {
                    ids[collection].Add(id);
                }

                references.AddRange(converted.References.Select(x => (collection, documentId, x)));
                people.AddRange(converted.People.Select(x => (collection, documentId, x)));
                after.Add(collection, document, converted.Id);
                if (
                    collection == IdPaths.EVENT_INFORMATIONS
                    && HistoricRankings.IsHistoric(document, now)
                    && Records.GuidOf(document, "_id") is { } ended
                )
                {
                    historic.Add(ended);
                }

                if (
                    collection == "event_rankings"
                    && !HistoricRankings.IsDeleted(document)
                    && Records.GuidOf(document, "EventId") is { } owner
                )
                {
                    rankings.Add(
                        (Records.GuidOf(document, "_id") ?? Guid.Empty, owner, HistoricRankings.StateOf(document))
                    );
                }
            }
        }

        integers.AddRange(await CollectionsWithNumbersAsync(database));
        var unresolved = references
            .Where(x => !ids.GetValueOrDefault(x.Reference.Target, []).Contains(x.Reference.Id))
            .Select(x =>
                $"{x.Source} {x.Document} {x.Reference.Path} names {x.Reference.Target} {x.Reference.Id}, which is not there"
            )
            .ToList();
        var strangers = people
            .Where(x =>
                !ids[x.Person.Kind == IdentityKind.Athlete ? IdPaths.ATHLETES : IdPaths.HORSES].Contains(x.Person.Id)
            )
            .Select(x =>
                $"{x.Source} {x.Document} {x.Person.Path} is {x.Person.Kind} {x.Person.Id}, which no Setup row is"
            )
            .ToList();
        return
        [
            Check("No integer id or reference remains", integers),
            Check("Every reference resolves", unresolved),
            Check("Every Athlete and Horse id is a Setup row's", strangers),
            RankingsCheck(rankings, historic, finalised),
            CountsCheck(before, after),
            NamesCheck(before, after),
        ];
    }

    /// <summary>The collections the table does not know that have a document with a number for an id: nothing converts those.</summary>
    internal static async Task<List<string>> CollectionsWithNumbersAsync(IMongoDatabase database)
    {
        var found = new List<string>();
        var numbers = new BsonDocument(
            "_id",
            new BsonDocument("$type", new BsonArray { "int", "long", "double", "decimal" })
        );
        foreach (
            var name in (await (await database.ListCollectionNamesAsync()).ToListAsync()).Order(StringComparer.Ordinal)
        )
        {
            if (name.StartsWith("system.", StringComparison.Ordinal) || IdPaths.Collections.Contains(name))
            {
                continue;
            }

            if (await database.GetCollection<BsonDocument>(name).Find(numbers).Limit(1).AnyAsync())
            {
                found.Add($"{name}: a document has a number for its id, and the table does not know the collection");
            }
        }

        return found;
    }

    static VerificationCheck RankingsCheck(
        List<(Guid Id, Guid Event, PlacingsState State)> rankings,
        HashSet<Guid> historic,
        RankingsReport? finalised
    )
    {
        var ofHistoric = rankings.Where(x => historic.Contains(x.Event)).ToList();
        var told = finalised == null ? [] : finalised.SomePlacingsStored.Concat(finalised.Refused).ToHashSet();
        var lacking = ofHistoric.Where(x => x.State != PlacingsState.Final && !told.Contains(x.Id)).ToList();
        var details = new List<string>();
        if (finalised == null)
        {
            var pending = ofHistoric.Count(x => x.State == PlacingsState.Unplaced);
            var partial = ofHistoric.Where(x => x.State == PlacingsState.Partial).ToList();
            details.Add(
                $"{pending} Rankings of Events that have ended have no placings, and are finalised by the apply."
            );
            details.AddRange(partial.Select(x => $"Ranking {x.Id} holds only some of its ranks, and is left alone."));
            return new VerificationCheck("Every Ranking of an Event that has ended has its placings", true, details);
        }

        details.AddRange(
            finalised.SomePlacingsStored.Select(x => $"Ranking {x} holds only some of its ranks, and was left alone.")
        );
        details.AddRange(
            finalised.Refused.Select(x =>
                $"Ranking {x} could not be finalised by the Results code, and was left alone."
            )
        );
        details.AddRange(lacking.Select(x => $"Ranking {x.Id} of Event {x.Event} has no placings."));
        return new VerificationCheck(
            "Every Ranking of an Event that has ended has its placings",
            lacking.Count == 0,
            Cut(details)
        );
    }

    static VerificationCheck CountsCheck(DataFacts before, DataFacts after)
    {
        var differences = before
            .Counts.Keys.Union(after.Counts.Keys)
            .Order(StringComparer.Ordinal)
            .Where(x => before.Counts.GetValueOrDefault(x) != after.Counts.GetValueOrDefault(x))
            .Select(x =>
                $"{x} had {before.Counts.GetValueOrDefault(x)} documents and has {after.Counts.GetValueOrDefault(x)}"
            )
            .ToList();
        return Check("The counts of Events, Participations, Rankings and Handouts are unchanged", differences);
    }

    static VerificationCheck NamesCheck(DataFacts before, DataFacts after)
    {
        var differences = before
            .Names.Keys.Union(after.Names.Keys)
            .Where(x => before.Names.GetValueOrDefault(x) != after.Names.GetValueOrDefault(x))
            .Select(x => $"{x.Collection} {x.Id} says other names than it did")
            .Order(StringComparer.Ordinal)
            .ToList();
        return Check("Names in Core are unchanged", differences);
    }

    static VerificationCheck Check(string name, List<string> failures)
    {
        return new VerificationCheck(name, failures.Count == 0, Cut(failures));
    }

    static IReadOnlyList<string> Cut(List<string> details)
    {
        return details.Count <= LISTED ? details : [.. details.Take(LISTED), $"... and {details.Count - LISTED} more"];
    }
}
