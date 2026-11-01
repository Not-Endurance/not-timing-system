using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Harness;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0009, ADR-0010, #637: worlds that nobody wrote by hand. People are spelled in both scripts and in variants, carry the ids of
/// before or 0, have a FEI ID or not, and are Setup rows or not; rides are made of them at random (the seed is the test's, so a
/// failure is a world that can be made again). Whatever the world, the apply converts it, passes its checks, changes nothing the
/// second time, and ends in the same state when it is stopped half way and run again.
/// </summary>
public sealed class IdentitiesApplyRandomWorldTests : IClassFixture<MongoFixture>
{
    static readonly string[] ATHLETES =
    [
        "Иван Петров",
        "Ivan Petrov",
        "IVAN PETROV",
        "Мария Георгиева",
        "Maria Georgieva",
        "Maria Georgiev",
        "Stefan Kolev",
        "Stephan Kolev",
        "Nikolay Ivanov",
        "Nikolai Ivanov",
        "Petar Dimitrov",
        "Todor Ivanov",
    ];
    static readonly string[] HORSES = ["Burya", "Буря", "Vihar", "Zora", "Thunderbolt", "Thunderbold", "Nightfall"];

    readonly MongoFixture _mongo;

    public IdentitiesApplyRandomWorldTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Whatever_the_world_is_it_converts_passes_its_checks_changes_nothing_twice_and_survives_being_stopped(
        int seed
    )
    {
        var whole = await WorldAsync(seed);
        var steps = new List<string>();
        var first = await ApplyAsync(whole, step => steps.Add(step));
        Assert.True(first.Succeeded, Describe(seed, first));
        var integers = new List<string>();
        foreach (var collection in LegacyWorldData.COLLECTIONS)
        {
            foreach (
                var document in await whole
                    .GetCollection<BsonDocument>(collection)
                    .Find(FilterDefinition<BsonDocument>.Empty)
                    .ToListAsync()
            )
            {
                IntegerIds(document, collection, "", integers);
            }
        }

        Assert.True(integers.Count == 0, $"seed {seed}: {string.Join("; ", integers.Take(5))}");
        var converted = await LegacyWorldData.SnapshotAsync(whole);

        var second = await ApplyAsync(whole);

        Assert.True(second.Succeeded, Describe(seed, second));
        Assert.Empty(second.Written);
        Assert.Equal(converted, await LegacyWorldData.SnapshotAsync(whole));
        foreach (var stop in new[] { 2, steps.Count / 2, steps.Count - 2 })
        {
            var stopped = await WorldAsync(seed);
            var seen = 0;
            await Assert.ThrowsAsync<StoppedException>(
                () =>
                    ApplyAsync(
                        stopped,
                        _ =>
                        {
                            if (++seen == stop)
                            {
                                throw new StoppedException();
                            }
                        }
                    )
            );

            var resumed = await ApplyAsync(stopped);

            Assert.True(resumed.Succeeded, $"after step {stop}: {Describe(seed, resumed)}");
            Assert.Equal(converted, await LegacyWorldData.SnapshotAsync(stopped));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Whatever_the_owner_decides_of_the_items_to_review_the_run_passes_changes_nothing_twice_and_survives_being_stopped(
        int seed
    )
    {
        var world = await WorldAsync(seed);
        var decisions = await DecideAsync(seed, world);
        var whole = await WorldAsync(seed);
        var steps = new List<string>();
        var first = await ApplyAsync(whole, step => steps.Add(step), decisions);
        if (first.Refusals.Count > 0)
        {
            // decisions can overrule one another or wait for a group that is held: the run says so and writes nothing
            Assert.All(first.Refusals, x => Assert.True(x.Contains("overruled") || x.Contains("waits"), x));
            Assert.Empty(first.Steps);
            return;
        }

        Assert.True(first.Succeeded, Describe(seed, first));
        var converted = await LegacyWorldData.SnapshotAsync(whole);

        var second = await ApplyAsync(whole, null, decisions);

        Assert.True(second.Succeeded, Describe(seed, second));
        Assert.Empty(second.Written);
        Assert.Equal(converted, await LegacyWorldData.SnapshotAsync(whole));
        foreach (var stop in new[] { 2, steps.Count / 2, steps.Count - 2 })
        {
            var stopped = await WorldAsync(seed);
            var seen = 0;
            await Assert.ThrowsAsync<StoppedException>(
                () =>
                    ApplyAsync(
                        stopped,
                        _ =>
                        {
                            if (++seen == stop)
                            {
                                throw new StoppedException();
                            }
                        },
                        decisions
                    )
            );

            var resumed = await ApplyAsync(stopped, null, decisions);

            Assert.True(resumed.Succeeded, $"after step {stop}: {Describe(seed, resumed)}");
            Assert.Equal(converted, await LegacyWorldData.SnapshotAsync(stopped));
        }
    }

    /// <summary>A random answer to every item the dry run lists: same, different, or still to decide.</summary>
    static async Task<IdentityDecisions> DecideAsync(int seed, IMongoDatabase world)
    {
        var random = new Random(seed * 7919);
        var plan = (await IdentitiesMigration.Read(world)).Plan(IdentityDecisions.None);
        var items = new Dictionary<string, string?>();
        foreach (var entry in plan.Kinds.SelectMany(x => x.Entries))
        {
            // a held group is decided more often than a pair, which keeps most worlds free of decisions that overrule one another
            var roll = random.Next(0, 100);
            var held = entry.Class == ReviewClass.Held;
            items[entry.Key] =
                roll < (held ? 40 : 12) ? "same"
                : roll < (held ? 60 : 24) ? "different"
                : null;
        }

        return IdentityDecisions.Parse(System.Text.Json.JsonSerializer.Serialize(new { items }));
    }

    static string Describe(int seed, ApplyResult result)
    {
        return $"seed {seed}: {string.Join("; ", result.Refusals.Concat(result.Checks.Where(x => !x.Passed).SelectMany(x => x.Details.Prepend(x.Name))))}";
    }

    static Task<ApplyResult> ApplyAsync(
        IMongoDatabase database,
        Action<string>? afterStep = null,
        IdentityDecisions? decisions = null
    )
    {
        return IdentitiesMigration.Apply(
            database,
            decisions ?? IdentityDecisions.None,
            new ApplyOptions
            {
                Now = LegacyWorldData.NOW,
                AfterStep = step =>
                {
                    afterStep?.Invoke(step);
                    return Task.CompletedTask;
                },
            }
        );
    }

    /// <summary>The integers where an id or a reference is.</summary>
    static void IntegerIds(BsonValue value, string collection, string path, List<string> found)
    {
        switch (value)
        {
            case BsonDocument document:
                foreach (var element in document.Elements)
                {
                    var child = path.Length == 0 ? element.Name : $"{path}.{element.Name}";
                    if (
                        (element.Name is "_id" or "Id" || element.Name.EndsWith("Id", StringComparison.Ordinal))
                        && element.Value is BsonInt32 or BsonInt64
                    )
                    {
                        found.Add($"{collection} {child} = {element.Value}");
                    }
                    else
                    {
                        IntegerIds(element.Value, collection, child, found);
                    }
                }

                break;
            case BsonArray array:
                foreach (var item in array)
                {
                    IntegerIds(item, collection, path, found);
                }

                break;
        }
    }

    async Task<IMongoDatabase> WorldAsync(int seed)
    {
        var random = new Random(seed);
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase("apply-" + Guid.NewGuid().ToString("N"));
        var bulgaria = LegacyWorldData.Country(1, "Bulgaria", "BG", "BUL");
        await Insert(database, "countries", bulgaria);
        var start = LegacyWorldData.NOW.UtcDateTime.AddDays(-60);
        await Insert(
            database,
            "event_informations",
            LegacyWorldData.Information(900, "One", start, start.AddDays(2)),
            LegacyWorldData.Information(901, "Two", start.AddDays(10), start.AddDays(12)),
            LegacyWorldData.Information(902, "Three", start.AddDays(20), start.AddDays(22))
        );

        var rows = new List<BsonDocument>();
        var rowIds = Enumerable.Range(1, 8).OrderBy(_ => random.Next()).ToList();
        foreach (var name in ATHLETES.OrderBy(_ => random.Next()).Take(random.Next(2, 8)))
        {
            rows.Add(Spell(random, rowIds[rows.Count], name, bulgaria));
        }

        if (rows.Count > 0)
        {
            await Insert(database, "athletes", [.. rows]);
        }

        var horseRows = HORSES
            .OrderBy(_ => random.Next())
            .Take(random.Next(1, 4))
            .Select((name, index) => LegacyWorldData.Person(20 + index, name))
            .ToArray();
        await Insert(database, "horses", horseRows);

        var rides = new List<BsonDocument>();
        var count = random.Next(10, 26);
        for (var i = 0; i < count; i++)
        {
            var athlete = Spell(
                random,
                random.Next(0, 4) == 0 ? 0 : random.Next(1, 9),
                ATHLETES[random.Next(ATHLETES.Length)],
                bulgaria
            );
            var horse = LegacyWorldData.Person(
                random.Next(0, 3) == 0 ? 0 : random.Next(20, 27),
                HORSES[random.Next(HORSES.Length)]
            );
            rides.Add(LegacyWorldData.Ride(100 + i, 900 + random.Next(0, 3), i + 1, athlete, horse));
        }

        await Insert(database, "event_participations", [.. rides]);
        return database;
    }

    /// <summary>A person as a row or a copy has it: the id of before, a FEI ID sometimes, a country and a club sometimes.</summary>
    static BsonDocument Spell(Random random, int id, string name, BsonDocument country)
    {
        var fei = random.Next(0, 6) switch
        {
            0 => "100",
            1 => "101",
            _ => null,
        };
        return LegacyWorldData.Person(
            id,
            name,
            fei: fei,
            country: random.Next(0, 2) == 0 ? country : null,
            club: random.Next(0, 3) == 0 ? "Sofia" : null
        );
    }

    static Task Insert(IMongoDatabase database, string collection, params BsonDocument[] documents)
    {
        return database.GetCollection<BsonDocument>(collection).InsertManyAsync(documents);
    }

    sealed class StoppedException : Exception;
}
