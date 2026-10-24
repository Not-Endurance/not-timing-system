using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>What <c>migrate-identities</c> reads before it plans: the copies of the Athletes and the Horses, and the Events they are in.</summary>
public sealed class IdentityInputs
{
    internal IdentityInputs(
        IReadOnlyList<IdentityCopy> copies,
        IReadOnlyList<BsonDocument> informations,
        IReadOnlyList<BsonDocument> setups
    )
    {
        Copies = copies;
        Informations = informations;
        Setups = setups;
    }

    public IReadOnlyList<IdentityCopy> Copies { get; }

    /// <summary>The documents of the Events that have started.</summary>
    public IReadOnlyList<BsonDocument> Informations { get; }

    /// <summary>The Setups, few: the report names an Event that has not started by them.</summary>
    public IReadOnlyList<BsonDocument> Setups { get; }

    public IdentityPlan Plan(IdentityDecisions decisions)
    {
        return IdentityPlan.Build(Copies, IdentityEvents.Labels(Informations, Setups), decisions);
    }
}

/// <summary>
/// <c>migrate-identities</c> (#636, #637, ADR-0009, ADR-0010). The dry run reads every collection that holds an Athlete or a Horse,
/// and the Events by name and date, and plans what the registry would become; it only reads. The documents are read a page at a time
/// and dropped once their copies are taken, because the Rankings of before ADR-0006 embed a whole Participation in every entry.
/// <see cref="Apply"/> converts every id to a Guid, merges the identities, finalises the Rankings of the Events that have ended and
/// verifies the result.
/// </summary>
public static class IdentitiesMigration
{
    public const string EVENT_INFORMATIONS = "event_informations";

    /// <param name="database">The database to read.</param>
    /// <param name="decisions">What the owner has decided so far.</param>
    /// <param name="pageSize">How many documents a read asks for.</param>
    /// <param name="wait">How a throttled read waits; the clock's delay unless a test gives its own.</param>
    public static async Task<IdentityPlan> Plan(
        IMongoDatabase database,
        IdentityDecisions decisions,
        int pageSize = ThrottledReads.PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null
    )
    {
        return (await Read(database, pageSize, wait)).Plan(decisions);
    }

    /// <summary>Reads the copies of the Athletes and the Horses, and the Events, from the database.</summary>
    public static async Task<IdentityInputs> Read(
        IMongoDatabase database,
        int pageSize = ThrottledReads.PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null
    )
    {
        var copies = new List<IdentityCopy>();
        var setups = new List<BsonDocument>();
        foreach (var name in IdentityCopies.All)
        {
            await foreach (var document in Documents(database, name, pageSize, wait))
            {
                copies.AddRange(IdentityCopies.Read(name, document));
                if (name == IdentityCopies.CONFIGURE_EVENTS)
                {
                    setups.Add(document); // few, and the report names an Event that has not started by them
                }
            }
        }

        var informations = new List<BsonDocument>();
        await foreach (var document in Documents(database, EVENT_INFORMATIONS, pageSize, wait))
        {
            informations.Add(document);
        }

        return new IdentityInputs(copies, informations, setups);
    }

    /// <summary>
    /// Converts every id and every reference of every collection to a Guid, merges the Athletes and the Horses that are the same, gives
    /// each identity a Setup row, stores the final placings of the Rankings of the Events that have ended, and verifies the result. Each
    /// step writes only what is not written yet, so a run that stopped finishes when it is run again, and a run on converted data
    /// changes nothing. It writes nothing when it is refused.
    /// </summary>
    public static Task<ApplyResult> Apply(IMongoDatabase database, IdentityDecisions decisions, ApplyOptions options)
    {
        return new IdentitiesApply(database, decisions, options).RunAsync();
    }

    /// <summary>
    /// What an apply would do, found without writing: what would refuse it, the Rankings of the Events that have ended and where their
    /// placings are (the ranks themselves are computed at apply, because the code that computes them reads only converted documents),
    /// and the verification run over the result the plan would give, converted in memory.
    /// </summary>
    public static async Task<PlannedCutover> Preview(
        IMongoDatabase database,
        IdentityInputs inputs,
        IdentityPlan plan,
        DateTimeOffset now,
        int pageSize = ThrottledReads.PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null
    )
    {
        var assignments = IdentityAssignments.Build(
            plan,
            IdentityEvents.Dates(inputs.Informations, inputs.Setups),
            await CountriesAsync(database, pageSize, wait)
        );
        var refusals = new List<string>(assignments.Refusals);
        var rankings = await HistoricRankings.Scan(database, now, pageSize, wait);
        var checks = new List<VerificationCheck>();
        if (refusals.Count == 0)
        {
            refusals.AddRange(await IdentitiesApply.UnconvertibleAsync(database, assignments, pageSize, wait));
            var before = await DataFacts.ReadAsync(database, pageSize, wait);
            checks.AddRange(
                await ApplyVerification.RunAsync(
                    database,
                    collection => Planned(database, collection, assignments, pageSize, wait),
                    before,
                    now,
                    finalised: null
                )
            );
        }

        return new PlannedCutover(refusals, rankings, checks);
    }

    internal static async Task<List<BsonDocument>> CountriesAsync(
        IMongoDatabase database,
        int pageSize,
        Func<TimeSpan, Task>? wait
    )
    {
        var countries = new List<BsonDocument>();
        await foreach (var country in Documents(database, "countries", pageSize, wait))
        {
            countries.Add(country);
        }

        return countries;
    }

    /// <summary>
    /// The documents of a collection as they would be once converted, and the rows to make there. A row that merges away is not left
    /// out: the checks look at a row by its id alone, and the identity's Guid is the id it would have.
    /// </summary>
    static async IAsyncEnumerable<BsonDocument> Planned(
        IMongoDatabase database,
        string collection,
        IdentityAssignments assignments,
        int pageSize,
        Func<TimeSpan, Task>? wait
    )
    {
        var kind = collection switch
        {
            IdPaths.ATHLETES => IdentityKind.Athlete,
            IdPaths.HORSES => IdentityKind.Horse,
            _ => (IdentityKind?)null,
        };
        var identities = kind == null ? [] : assignments.Identities.Where(x => x.Kind == kind).ToList();
        await foreach (var document in Documents(database, collection, pageSize, wait))
        {
            yield return IdConversion.Convert(collection, document, assignments).Document;
        }

        foreach (var identity in identities.Where(x => x.Create != null))
        {
            var row = new BsonDocument("_id", new BsonBinaryData(identity.Id, GuidRepresentation.Standard));
            row.AddRange(identity.Create!);
            yield return IdConversion.Convert(collection, row, assignments).Document;
        }
    }

    static IAsyncEnumerable<BsonDocument> Documents(
        IMongoDatabase database,
        string collection,
        int pageSize,
        Func<TimeSpan, Task>? wait
    )
    {
        return ThrottledReads.Documents(database.GetCollection<BsonDocument>(collection), pageSize, wait);
    }
}
