using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.ParticipationCopies;

/// <summary>
/// What the migration does to the documents, worked out before anything is written: the new documents to store, and the
/// report of the counts, the Participations that are missing and the copies that differ. It stores them on request.
/// </summary>
internal sealed class ParticipationCopiesPlan
{
    const string ID = "_id";
    const string EVENT_ID = "EventId";
    const string ENTRIES = "Entries";
    const string COPY = "Participation";
    const string REFERENCE = "ParticipationId";
    const string IS_NOT_RANKED = "IsNotRanked";
    const string RANK = "Rank";

    readonly ParticipationCopiesReport _report;
    readonly Dictionary<string, BsonDocument> _stored;
    readonly List<(string Collection, BsonDocument Document)> _changes = [];

    public ParticipationCopiesPlan(ParticipationCopiesReport report, IEnumerable<BsonDocument> participations)
    {
        _report = report;
        _stored = participations.ToDictionary(x => KeyOf(x[ID]));
    }

    /// <summary>Each entry that holds a copy of a Participation becomes the id of it, with its mark and its rank.</summary>
    public void Rankings(IReadOnlyList<BsonDocument> rankings)
    {
        var changed = 0;
        var converted = 0;
        foreach (var ranking in rankings)
        {
            if (!ranking.TryGetValue(ENTRIES, out var value) || value is not BsonArray entries)
            {
                continue;
            }

            var rewritten = new BsonArray();
            var named = new List<BsonValue>();
            var convertedHere = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                var id = Resolve(ParticipationCopiesMigration.RANKINGS, ranking, $"{ENTRIES}[{index}]", entries[index]);
                if (id != null)
                {
                    named.Add(id);
                }

                if (!IsCopy(entries[index]))
                {
                    rewritten.Add(entries[index]);
                    continue;
                }

                convertedHere++;
                rewritten.Add(id == null ? entries[index] : EntryAsReference(entries[index].AsBsonDocument, id));
            }

            ReportRepeated(ranking, named);
            if (convertedHere == 0)
            {
                continue;
            }

            changed++;
            converted += convertedHere;
            var migrated = ranking.DeepClone().AsBsonDocument;
            migrated[ENTRIES] = rewritten;
            _changes.Add((ParticipationCopiesMigration.RANKINGS, migrated));
        }

        _report.Collections.Add(
            new CollectionCounts(ParticipationCopiesMigration.RANKINGS, rankings.Count, changed, converted)
        );
    }

    /// <summary>A Handout that holds a copy of a Participation holds the id of it, where the copy was.</summary>
    public void Handouts(IReadOnlyList<BsonDocument> handouts)
    {
        var changed = 0;
        foreach (var handout in handouts)
        {
            var id = Resolve(ParticipationCopiesMigration.HANDOUTS, handout, COPY, handout);
            if (!IsCopy(handout))
            {
                continue;
            }

            changed++;
            if (id != null)
            {
                _changes.Add((ParticipationCopiesMigration.HANDOUTS, HandoutAsReference(handout, id)));
            }
        }

        _report.Collections.Add(
            new CollectionCounts(ParticipationCopiesMigration.HANDOUTS, handouts.Count, changed, 0)
        );
    }

    /// <summary>A Participation that carries its Total or a derived value of a Phase loses them.</summary>
    public void Participations(IReadOnlyList<BsonDocument> participations)
    {
        var changed = 0;
        foreach (var participation in participations)
        {
            if (!DerivedValues.AreIn(participation))
            {
                continue;
            }

            changed++;
            _changes.Add((ParticipationCopiesMigration.PARTICIPATIONS, DerivedValues.Without(participation)));
        }

        _report.Collections.Add(
            new CollectionCounts(ParticipationCopiesMigration.PARTICIPATIONS, participations.Count, changed, 0)
        );
    }

    public async Task Persist(IMongoDatabase database)
    {
        foreach (var (collection, document) in _changes)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(ID, document[ID]);
            var result = await database.GetCollection<BsonDocument>(collection).ReplaceOneAsync(filter, document);
            if (result.MatchedCount != 1)
            {
                throw new InvalidOperationException(
                    $"{collection} {ParticipationCopiesReport.Describe(document[ID])} was not found to replace. "
                        + "Run the migration again: what it has done stays done."
                );
            }
        }
    }

    /// <summary>
    /// The id of the Participation the holder (an entry of a Ranking, or a Handout) names. A Participation that is not
    /// there is reported as missing, and so is one of another Event; a copy that differs from the stored one is reported
    /// with the fields that differ.
    /// </summary>
    BsonValue? Resolve(string collection, BsonDocument owner, string where, BsonValue holder)
    {
        BsonDocument? copy = null;
        BsonValue? id = null;
        if (holder is BsonDocument document)
        {
            if (document.TryGetValue(COPY, out var embedded) && embedded is BsonDocument embeddedCopy)
            {
                copy = embeddedCopy;
                id = copy.TryGetValue(ID, out var copyId) ? copyId : null;
            }
            else if (document.TryGetValue(REFERENCE, out var reference))
            {
                id = reference;
            }
        }

        if (id == null || id.IsBsonNull)
        {
            Miss(collection, owner, where, null, "names no Participation");
            return null;
        }

        if (!_stored.TryGetValue(KeyOf(id), out var stored))
        {
            Miss(collection, owner, where, id, "not in the Participations collection");
            return null;
        }

        if (IsOfAnotherEvent(owner, stored))
        {
            Miss(collection, owner, where, id, "belongs to another Event");
            return null;
        }

        var fields = copy == null ? [] : CopyDifferences.Between(copy, stored);
        if (fields.Count > 0)
        {
            _report.Differing.Add(new DifferingCopy(collection, owner[ID], where, id, fields));
        }

        return id;
    }

    /// <summary>A Participation that two entries of one Ranking name: the application does not load such a Ranking.</summary>
    void ReportRepeated(BsonDocument ranking, List<BsonValue> named)
    {
        foreach (var repeated in named.GroupBy(KeyOf).Where(x => x.Count() > 1))
        {
            _report.Repeated.Add(
                new RepeatedParticipation(ParticipationCopiesMigration.RANKINGS, ranking[ID], repeated.First())
            );
        }
    }

    void Miss(string collection, BsonDocument owner, string where, BsonValue? id, string reason)
    {
        _report.Missing.Add(new MissingParticipation(collection, owner[ID], where, id, reason));
    }

    static bool IsCopy(BsonValue holder)
    {
        return holder is BsonDocument document && document.TryGetValue(COPY, out var copy) && copy is BsonDocument;
    }

    /// <summary>
    /// The entry of a Ranking with the id in place of the copy, the mark when it is set and the rank when there is one:
    /// as the application writes an entry, a default is not written.
    /// </summary>
    static BsonDocument EntryAsReference(BsonDocument entry, BsonValue id)
    {
        var migrated = new BsonDocument { [REFERENCE] = id };
        if (entry.TryGetValue(IS_NOT_RANKED, out var isNotRanked) && isNotRanked.IsBoolean && isNotRanked.AsBoolean)
        {
            migrated[IS_NOT_RANKED] = true;
        }

        if (entry.TryGetValue(RANK, out var rank) && !rank.IsBsonNull)
        {
            migrated[RANK] = rank;
        }

        return migrated;
    }

    /// <summary>The Handout with the id where the copy was, and everything else as it was.</summary>
    static BsonDocument HandoutAsReference(BsonDocument handout, BsonValue id)
    {
        var migrated = new BsonDocument();
        foreach (var element in handout)
        {
            if (element.Name == COPY)
            {
                migrated[REFERENCE] = id;
            }
            else
            {
                migrated[element.Name] = element.Value.DeepClone();
            }
        }

        return migrated;
    }

    static bool IsOfAnotherEvent(BsonDocument owner, BsonDocument stored)
    {
        return owner.TryGetValue(EVENT_ID, out var ownerEvent)
            && stored.TryGetValue(EVENT_ID, out var storedEvent)
            && !ownerEvent.IsBsonNull
            && !storedEvent.IsBsonNull
            && KeyOf(ownerEvent) != KeyOf(storedEvent);
    }

    /// <summary>An id as a key, whatever its type: the integers of before, a Guid, an ObjectId.</summary>
    static string KeyOf(BsonValue id)
    {
        return id switch
        {
            BsonBinaryData binary => $"binary:{binary.SubType}:{Convert.ToHexString(binary.Bytes)}",
            { IsNumeric: true } => $"number:{id.ToDecimal()}",
            _ => $"{id.BsonType}:{id}",
        };
    }
}
