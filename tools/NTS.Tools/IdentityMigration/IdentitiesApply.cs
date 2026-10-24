using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.Shared;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// One run of <c>migrate-identities --apply</c> (#637, ADR-0009, ADR-0010). It makes the assignments from the data as it is and keeps
/// them in the database, or finds the ones a run that stopped kept; refuses, writing nothing, what the data and the decisions do not
/// agree on or the table cannot convert; converts the collections one after the other, each document with a write of its own (the
/// Guid-keyed twin is written, then the original is deleted); makes the rows and merges them; deletes the kept assignments; stores the
/// placings of the Rankings of the Events that have ended; and verifies. It assumes no transaction and retries a throttled request.
/// </summary>
internal sealed class IdentitiesApply
{
    const string SETUP_ATHLETES = "athletes";
    const string SETUP_HORSES = "horses";
    const int LISTED = 12;
    static readonly string[] ORDER =
    [
        "countries",
        "clubs",
        "users",
        IdPaths.EVENT_INFORMATIONS,
        "configure_events",
        "event_officials",
        "event_operators",
        IdPaths.PARTICIPATIONS,
        "event_rankings",
        "event_handouts",
        "event_user_sessions",
        SETUP_ATHLETES,
        SETUP_HORSES,
    ];

    readonly IMongoDatabase _database;
    readonly IdentityDecisions _decisions;
    readonly ApplyOptions _options;
    readonly IdentityPlanStore _store;
    readonly ApplyResult _result = new();

    public IdentitiesApply(IMongoDatabase database, IdentityDecisions decisions, ApplyOptions options)
    {
        _database = database;
        _decisions = decisions;
        _options = options;
        _store = new IdentityPlanStore(database, options.Wait);
    }

    /// <summary>What the table cannot convert, found before anything is written: every document is converted in memory.</summary>
    internal static async Task<List<string>> UnconvertibleAsync(
        IMongoDatabase database,
        IdentityAssignments assignments,
        int pageSize,
        Func<TimeSpan, Task>? wait
    )
    {
        var refusals = new List<string>();
        var problems = 0;
        foreach (var collection in ORDER)
        {
            await foreach (
                var document in ThrottledReads.Documents(
                    database.GetCollection<BsonDocument>(collection),
                    pageSize,
                    wait
                )
            )
            {
                foreach (var problem in IdConversion.Convert(collection, document, assignments).Problems)
                {
                    if (++problems <= LISTED)
                    {
                        refusals.Add($"{problem.Collection} {problem.DocumentId} {problem.Path}: {problem.Message}");
                    }
                }
            }
        }

        if (problems > LISTED)
        {
            refusals.Add($"... and {problems - LISTED} more ids that the table cannot convert.");
        }

        refusals.AddRange(await ApplyVerification.CollectionsWithNumbersAsync(database));
        return refusals;
    }

    public async Task<ApplyResult> RunAsync()
    {
        var before = await DataFacts.ReadAsync(_database, _options.PageSize, _options.Wait);
        if (await PlanAsync() is not { } assignments)
        {
            return _result;
        }

        await StepAsync("plan");
        foreach (var collection in ORDER)
        {
            if (!await ConvertAsync(collection, assignments))
            {
                return _result;
            }

            await StepAsync(collection);
        }

        await DeleteMergedRowsAsync(assignments);
        await StepAsync("merged-rows");
        await _store.DeleteAsync();
        await StepAsync("plan-consumed");
        _result.Rankings = await HistoricRankings.FinaliseAsync(
            _database,
            _options.Now,
            _options.PageSize,
            _options.Wait,
            _options.BeforeRankingWritten
        );
        await StepAsync("rankings");
        _result.Checks.AddRange(
            await ApplyVerification.RunAsync(
                _database,
                collection => ThrottledReads.Documents(Documents(collection), _options.PageSize, _options.Wait),
                before,
                _options.Now,
                _result.Rankings
            )
        );
        return _result;
    }

    /// <summary>The assignments of a run that stopped, or new ones, made and kept; none when the run is refused, which writes nothing.</summary>
    async Task<IdentityAssignments?> PlanAsync()
    {
        var kept = await _store.LoadAsync();
        if (kept != null)
        {
            if (kept.Decisions != _decisions.Fingerprint())
            {
                _result.Refusals.Add(
                    "A run that stopped is kept in the database, and it was made with other decisions than the file has now. "
                        + "Put the decisions file back as it was, or restore the backup and start again."
                );
                return null;
            }

            _result.Resumed = true;
            return kept;
        }

        var inputs = await IdentitiesMigration.Read(_database, _options.PageSize, _options.Wait);
        var plan = inputs.Plan(_decisions);
        if (_options.PlanMade != null)
        {
            await _options.PlanMade(plan);
        }

        var assignments = IdentityAssignments.Build(
            plan,
            IdentityEvents.Dates(inputs.Informations, inputs.Setups),
            await IdentitiesMigration.CountriesAsync(_database, _options.PageSize, _options.Wait)
        );
        _result.Refusals.AddRange(assignments.Refusals);
        if (assignments.Refusals.Count == 0)
        {
            _result.Refusals.AddRange(
                await UnconvertibleAsync(_database, assignments, _options.PageSize, _options.Wait)
            );
        }

        if (_result.Refusals.Count > 0)
        {
            return null;
        }

        await _store.SaveAsync(assignments.ToDocument(_decisions.Fingerprint()));
        return assignments;
    }

    /// <returns>Whether the collection is converted; if not, the run is refused for what the table could not convert.</returns>
    async Task<bool> ConvertAsync(string collection, IdentityAssignments assignments)
    {
        var documents = Documents(collection);
        var kind = collection switch
        {
            SETUP_ATHLETES => IdentityKind.Athlete,
            SETUP_HORSES => IdentityKind.Horse,
            _ => (IdentityKind?)null,
        };
        var merged = new HashSet<string>(StringComparer.Ordinal);
        var fills = new Dictionary<string, string>(StringComparer.Ordinal);
        if (kind is { } registry)
        {
            await MakeRowsAsync(collection, registry, assignments);
            var identities = assignments.Identities.Where(x => x.Kind == registry).ToList();
            merged.UnionWith(identities.SelectMany(x => x.Merged));
            foreach (var identity in identities.Where(x => x.Survivor != null && x.NameEnglish != null))
            {
                fills[identity.Survivor!] = identity.NameEnglish!;
            }
        }

        foreach (var batch in (await IdsAsync(documents)).Chunk(_options.PageSize))
        {
            var found = await ThrottledReads.Retried(
                () => documents.Find(Builders<BsonDocument>.Filter.In("_id", batch)).ToListAsync(),
                _options.Wait
            );
            foreach (var document in found)
            {
                var documentId = IdentityCopies.Describe(document["_id"]);
                if (merged.Contains(documentId))
                {
                    continue; // deleted once its copies are re-pointed
                }

                var converted = IdConversion.Convert(collection, document, assignments);
                if (converted.Problems.Count > 0)
                {
                    _result.Refusals.AddRange(
                        converted
                            .Problems.Take(LISTED)
                            .Select(x => $"{x.Collection} {x.DocumentId} {x.Path}: {x.Message}")
                    );
                    return false;
                }

                var result = converted.Document;
                if (fills.TryGetValue(documentId, out var english) && IsBlank(result, "NameEnglish"))
                {
                    result["NameEnglish"] = english;
                }

                if (result.Equals(document))
                {
                    continue;
                }

                await WriteAsync(documents, document["_id"], result);
                Written(collection);
            }
        }

        return true;
    }

    /// <summary>Makes the rows of the identities that have none. A row is made once: one that is there, from a run that stopped, is left as it is.</summary>
    async Task MakeRowsAsync(string collection, IdentityKind kind, IdentityAssignments assignments)
    {
        var documents = Documents(collection);
        foreach (var identity in assignments.Identities.Where(x => x.Kind == kind && x.Create != null))
        {
            var row = new BsonDocument("_id", Records.Uuid(identity.Id));
            row.AddRange(identity.Create!);
            var converted = IdConversion.Convert(collection, row, assignments).Document;
            try
            {
                await ThrottledReads.Retried(() => documents.InsertOneAsync(converted), _options.Wait);
                _result.RowsMade++;
                Written(collection);
            }
            catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // made by a run that stopped, and since then perhaps corrected by hand: it is left as it is
            }
        }
    }

    /// <summary>The Guid-keyed twin is written, then the original is deleted: a document whose id stays is replaced in place.</summary>
    async Task WriteAsync(IMongoCollection<BsonDocument> documents, BsonValue original, BsonDocument converted)
    {
        var twin = converted["_id"];
        await ThrottledReads.Retried(
            () =>
                documents.ReplaceOneAsync(
                    new BsonDocument("_id", twin),
                    converted,
                    new ReplaceOptions { IsUpsert = true }
                ),
            _options.Wait
        );
        if (!twin.Equals(original))
        {
            await ThrottledReads.Retried(
                () => documents.DeleteOneAsync(new BsonDocument("_id", original)),
                _options.Wait
            );
        }
    }

    /// <summary>The Setup rows that are another row's duplicate are deleted last, when nothing refers to them any more.</summary>
    async Task DeleteMergedRowsAsync(IdentityAssignments assignments)
    {
        foreach (
            var (collection, kind) in new[]
            {
                (SETUP_ATHLETES, IdentityKind.Athlete),
                (SETUP_HORSES, IdentityKind.Horse),
            }
        )
        {
            var merged = assignments
                .Identities.Where(x => x.Kind == kind)
                .SelectMany(x => x.Merged)
                .ToHashSet(StringComparer.Ordinal);
            if (merged.Count == 0)
            {
                continue;
            }

            var documents = Documents(collection);
            foreach (var id in (await IdsAsync(documents)).Where(x => merged.Contains(IdentityCopies.Describe(x))))
            {
                await ThrottledReads.Retried(
                    () => documents.DeleteOneAsync(new BsonDocument("_id", id)),
                    _options.Wait
                );
                _result.RowsMerged++;
            }
        }
    }

    /// <summary>The ids of the documents as they are now: the documents are written and deleted while they are gone through, so a page of them would skip some.</summary>
    Task<List<BsonValue>> IdsAsync(IMongoCollection<BsonDocument> documents)
    {
        return ThrottledReads.Retried(
            async () =>
                (
                    await documents
                        .Find(FilterDefinition<BsonDocument>.Empty)
                        .Project(new BsonDocument("_id", 1))
                        .ToListAsync()
                )
                    .Select(x => x["_id"])
                    .ToList(),
            _options.Wait
        );
    }

    async Task StepAsync(string step)
    {
        _result.Steps.Add(step);
        if (_options.AfterStep != null)
        {
            await _options.AfterStep(step);
        }
    }

    void Written(string collection)
    {
        _result.Written[collection] = _result.Written.GetValueOrDefault(collection) + 1;
    }

    IMongoCollection<BsonDocument> Documents(string collection)
    {
        return _database.GetCollection<BsonDocument>(collection);
    }

    static bool IsBlank(BsonDocument document, string field)
    {
        return !document.TryGetValue(field, out var value)
            || value.IsBsonNull
            || (value.IsString && string.IsNullOrWhiteSpace(value.AsString));
    }
}
