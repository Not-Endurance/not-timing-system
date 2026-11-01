using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The Athletes and the Horses as the data of before the registry stores them (#636): the integers of before as ids, 0 on a copy
/// that had none, names in Bulgarian in some documents and in Latin in others, and every collection that holds one. The people are
/// made up. Bulgaria is entered as BG/BUL in Setup and as BGR in a ride.
/// </summary>
internal static class IdentityRegistryData
{
    public static readonly string[] COLLECTIONS =
    [
        "athletes",
        "horses",
        "configure_events",
        "event_informations",
        "event_participations",
        "event_rankings",
        "event_handouts",
    ];

    public static BsonDocument Person(
        int id,
        string name,
        string? nameEnglish = null,
        string? fei = null,
        BsonDocument? country = null,
        string? club = null
    )
    {
        var person = new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["Name"] = name,
        };
        if (nameEnglish != null)
        {
            person["NameEnglish"] = nameEnglish;
        }

        if (fei != null)
        {
            person["FeiId"] = fei;
        }

        if (country != null)
        {
            person["Country"] = country;
        }

        if (club != null)
        {
            person["Club"] = new BsonDocument { ["_id"] = 7, ["Name"] = club };
        }

        return person;
    }

    public static BsonDocument Bulgaria(string isoCode, string? nfCode = null)
    {
        var country = new BsonDocument
        {
            ["_id"] = 1,
            ["Name"] = "Bulgaria",
            ["IsoCode"] = isoCode,
        };
        if (nfCode != null)
        {
            country["NfCode"] = nfCode;
        }

        return country;
    }

    /// <summary>The Athletes and the Horses of two finished Events and a Setup, with everything the migration has to meet.</summary>
    public static async Task SeedAsync(IMongoDatabase database)
    {
        var setupBulgaria = Bulgaria("BG", "BUL");
        await Insert(
            database,
            "athletes",
            Person(1, "Иван Петров", "Ivan Petrov", country: setupBulgaria, club: "Sofia"), // both scripts: he bridges the two copies below
            Person(4, "Maria Georgieva"),
            Person(5, "Maria Georgieva"), // a Setup duplicate
            Person(6, "Nikola Todorov", fei: "400", country: setupBulgaria),
            Person(7, "Stefan Kolev", fei: "100"),
            Person(8, "Stefan Kolev", fei: "101"), // one digit from the FEI ID above: a conflict
            Person(10, "Nikola Todorow") // the same letters as Nikola Todorov, spelled otherwise
        );
        await Insert(database, "horses", Person(20, "Burya"), Person(30, "Thunderbolt"));
        await Insert(
            database,
            "event_informations",
            Information(
                900,
                "Spring ride",
                new DateTime(2026, 4, 25, 21, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 28, 20, 59, 59, DateTimeKind.Utc)
            ),
            Information(
                901,
                "Autumn ride",
                new DateTime(2025, 8, 31, 21, 0, 0, DateTimeKind.Utc),
                new DateTime(2025, 9, 1, 20, 59, 59, DateTimeKind.Utc)
            )
        );

        var ride70 = Ride(70, 900, 1, Person(0, "Иван Петров"), Person(20, "Burya"));
        var ride71 = Ride(71, 900, 2, Person(0, "IVAN PETROV"), Person(21, "Vihar"));
        var ride72 = Ride(72, 900, 3, Person(9, "Petar Dimitrov"), Person(22, "Zora")); // no Setup row
        await Insert(
            database,
            "event_participations",
            ride70,
            ride71,
            ride72,
            Ride(73, 900, 4, Person(0, "Maria Georgieva"), Person(23, "Thunderbold")), // the id 0, and a Horse a letter from Thunderbolt
            Ride(74, 900, 5, Person(0, "Todor Ivanov"), Person(0, "Nightfall")), // the id 0 on both
            Ride(75, 900, 6, Person(6, "Nikola Todorov", fei: "400", country: Bulgaria("BGR")), Person(24, "Zora")),
            Ride(80, 901, 1, Person(9, "Petar Dimitrov"), Person(22, "Zora")),
            Ride(81, 901, 2, Person(0, "Yordan Ivanov", fei: "300"), Person(25, "Vihar")), // one FEI ID, two spellings
            Ride(82, 901, 3, Person(0, "Iordan Ivanov", fei: "300"), Person(26, "Burya"))
        );
        await Insert(
            database,
            "event_rankings",
            Ranking(300, 900, ride70, ride71),
            Ranking(301, 900, ride70) // the same Participation counted in a second Ranking
        );
        await Insert(
            database,
            "event_handouts",
            new BsonDocument
            {
                ["_id"] = 400,
                ["EventId"] = 900,
                ["Participation"] = ride72.DeepClone(),
            }
        );
        await Insert(
            database,
            "configure_events",
            new BsonDocument
            {
                ["_id"] = 902,
                ["TenantId"] = "nts",
                ["Name"] = "Winter ride",
                ["Combinations"] = new BsonArray
                {
                    Combination(1, Person(7, "Stefan Kolev", fei: "100"), Person(30, "Thunderbolt")),
                },
                ["Competitions"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["_id"] = 500,
                        ["Name"] = "CEI 1*",
                        ["Start"] = new BsonDateTime(new DateTime(2026, 12, 4, 7, 0, 0, DateTimeKind.Utc)),
                        ["Participations"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["_id"] = 600,
                                ["Combination"] = Combination(
                                    2,
                                    Person(8, "Stefan Kolev", fei: "101"),
                                    Person(31, "Zora")
                                ),
                            },
                        },
                    },
                },
            }
        );
    }

    /// <summary>Every document of every collection as text, so that a test can tell that a run changed nothing.</summary>
    public static async Task<SortedDictionary<string, string[]>> SnapshotAsync(IMongoDatabase database)
    {
        var all = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var name in await (await database.ListCollectionNamesAsync()).ToListAsync())
        {
            var documents = await database
                .GetCollection<BsonDocument>(name)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .ToListAsync();
            all[name] = [.. documents.Select(x => x.ToJson()).Order(StringComparer.Ordinal)];
        }

        return all;
    }

    static BsonDocument Information(int id, string name, DateTime start, DateTime end)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["Name"] = name,
            ["StartDay"] = new BsonDateTime(start),
            ["EndDay"] = new BsonDateTime(end),
        };
    }

    static BsonDocument Combination(int number, BsonDocument athlete, BsonDocument horse)
    {
        return new BsonDocument
        {
            ["_id"] = 1000 + number,
            ["Number"] = number,
            ["Athlete"] = athlete,
            ["Horse"] = horse,
        };
    }

    static BsonDocument Ride(int id, int eventId, int number, BsonDocument athlete, BsonDocument horse)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["EventId"] = eventId,
            ["Category"] = "Senior",
            ["Competition"] = new BsonDocument { ["Name"] = "CEI 1*", ["Ruleset"] = "FEI" },
            ["Combination"] = Combination(number, athlete, horse),
        };
    }

    static BsonDocument Ranking(int id, int eventId, params BsonDocument[] copies)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = "nts",
            ["EventId"] = eventId,
            ["Name"] = "CEI 1*",
            ["Entries"] = new BsonArray(
                copies.Select(x => new BsonDocument { ["Participation"] = x.DeepClone(), ["IsNotRanked"] = false })
            ),
        };
    }

    static async Task Insert(IMongoDatabase database, string collection, params BsonDocument[] documents)
    {
        await database.GetCollection<BsonDocument>(collection).InsertManyAsync(documents);
    }
}
