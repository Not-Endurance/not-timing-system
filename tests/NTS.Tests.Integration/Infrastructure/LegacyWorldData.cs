using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The database as the cutover finds it (#637): every collection in the shape the code reads, with the integers of before as ids and
/// as references, after the other migrations have run (a Ranking entry names its Participation, a Phase holds time events). It is
/// made up. Two Events have ended, and a third is Live; a Setup has not started. A person is in several spellings and several Events.
/// </summary>
internal static class LegacyWorldData
{
    public const string TENANT = "country-bg";
    public static readonly DateTimeOffset NOW = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    public static readonly string[] COLLECTIONS =
    [
        "countries",
        "clubs",
        "users",
        "athletes",
        "horses",
        "configure_events",
        "event_informations",
        "event_officials",
        "event_operators",
        "event_participations",
        "event_rankings",
        "event_handouts",
        "event_user_sessions",
    ];

    public static BsonDocument Country(int id, string name, string iso, string federation)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = TENANT,
            ["Name"] = name,
            ["IsoCode"] = iso,
            ["NfCode"] = federation,
        };
    }

    /// <summary>An Athlete or a Horse as Setup stores it, or as a ride copies it.</summary>
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
            ["TenantId"] = TENANT,
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

    /// <summary>An Athlete with the <c>User</c> that older documents stored in it and nothing reads.</summary>
    public static BsonDocument WithUser(BsonDocument athlete)
    {
        athlete["User"] = new BsonDocument { ["_id"] = 3, ["Email"] = "official@example.test" };
        return athlete;
    }

    /// <summary>The Combination of a ride: the number is the one of the start list, the id is made of it.</summary>
    public static BsonDocument Combination(int number, BsonDocument athlete, BsonDocument horse)
    {
        return new BsonDocument
        {
            ["_id"] = 1000 + number,
            ["Number"] = number,
            ["Athlete"] = athlete.DeepClone(),
            ["Horse"] = horse.DeepClone(),
        };
    }

    /// <summary>A Participation of an Event that has started, with two Phases, the first with a time event that was made as a Guid.</summary>
    public static BsonDocument Ride(int id, int eventId, int number, BsonDocument athlete, BsonDocument horse)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = TENANT,
            ["EventId"] = eventId,
            ["Category"] = "Senior",
            ["Competition"] = new BsonDocument { ["Name"] = "CEI 1*", ["Ruleset"] = "FEI" },
            ["Combination"] = Combination(number, athlete, horse),
            ["Phases"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = 5,
                    ["Gate"] = "A",
                    ["Length"] = 40.0,
                    ["Events"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["_id"] = new BsonBinaryData(
                                Guid.Parse("00000000-0000-4000-8000-0000000000" + (10 + number)),
                                GuidRepresentation.Standard
                            ),
                            ["Kind"] = "Arrived",
                        },
                    },
                },
                new BsonDocument
                {
                    ["_id"] = 6,
                    ["Gate"] = "B",
                    ["Length"] = 20.0,
                },
            },
        };
    }

    /// <summary>A Ranking whose entries name their Participations; a rank of 0 is not stored.</summary>
    public static BsonDocument Ranking(int id, int eventId, params (int Participation, int Rank)[] entries)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = TENANT,
            ["EventId"] = eventId,
            ["Name"] = "CEI 1*",
            ["Ruleset"] = "FEI",
            ["Category"] = "Senior",
            ["Entries"] = new BsonArray(
                entries.Select(x =>
                {
                    var entry = new BsonDocument { ["ParticipationId"] = x.Participation };
                    if (x.Rank > 0)
                    {
                        entry["Rank"] = x.Rank;
                    }

                    return entry;
                })
            ),
        };
    }

    public static BsonDocument Information(int id, string name, DateTime start, DateTime end)
    {
        return new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = TENANT,
            ["Name"] = name,
            ["Location"] = "Sofia",
            ["Country"] = Country(1, "Bulgaria", "BG", "BUL"),
            ["StartDay"] = new BsonDateTime(start),
            ["EndDay"] = new BsonDateTime(end),
        };
    }

    /// <summary>The people and the Events of the data: what the tests of the cutover read.</summary>
    public static async Task SeedAsync(IMongoDatabase database)
    {
        var bulgaria = Country(1, "Bulgaria", "BG", "BUL");
        await Insert(database, "countries", bulgaria, Country(2, "Romania", "RO", "ROU"));
        await Insert(
            database,
            "clubs",
            new BsonDocument
            {
                ["_id"] = 7,
                ["TenantId"] = TENANT,
                ["Name"] = "Sofia",
            }
        );
        await Insert(
            database,
            "users",
            new BsonDocument
            {
                ["_id"] = 3,
                ["TenantId"] = TENANT,
                ["Email"] = "official@example.test",
                ["Name"] = "Judge Example",
            }
        );
        await Insert(
            database,
            "athletes",
            Person(1, "Иван Петров", "Ivan Petrov", country: bulgaria, club: "Sofia"), // in both scripts: he bridges the two copies below
            Person(4, "Maria Georgieva"),
            Person(5, "Maria Georgieva"), // a Setup duplicate
            Person(6, "Nikola Todorov", fei: "400", country: bulgaria),
            WithUser(Person(7, "Stefan Kolev", fei: "100")), // and the User of an Athlete that #635 retired, which stays as it is
            Person(8, "Stefan Kolev", fei: "101"), // one digit from the FEI ID above: a conflict
            Person(10, "Nikola Todorow"), // the same letters as Nikola Todorov, spelled otherwise
            Person(11, "Георги Иванов") // with no English name, and a ride that spells it in Latin
        );
        await Insert(database, "horses", Person(20, "Burya"), Person(30, "Thunderbolt"));

        var spring = new DateTime(2026, 4, 25, 21, 0, 0, DateTimeKind.Utc);
        var autumn = new DateTime(2025, 8, 31, 21, 0, 0, DateTimeKind.Utc);
        await Insert(
            database,
            "event_informations",
            Information(900, "Spring ride", spring, new DateTime(2026, 4, 28, 20, 59, 59, DateTimeKind.Utc)),
            Information(901, "Autumn ride", autumn, new DateTime(2025, 9, 1, 20, 59, 59, DateTimeKind.Utc)),
            Information(903, "Live ride", NOW.UtcDateTime.AddDays(-1), NOW.UtcDateTime.AddDays(2))
        );
        await Insert(
            database,
            "event_officials",
            new BsonDocument
            {
                ["_id"] = 12,
                ["TenantId"] = TENANT,
                ["EventId"] = 900,
                ["Name"] = "Judge Example",
                ["Role"] = "Steward",
                ["UserId"] = 3,
            },
            new BsonDocument
            {
                ["_id"] = 14,
                ["TenantId"] = TENANT,
                ["EventId"] = 900,
                ["Name"] = "An Official Not Linked",
                ["Role"] = "VeterinaryCommissionMember",
            }
        );
        await Insert(
            database,
            "event_operators",
            new BsonDocument
            {
                ["_id"] = 13,
                ["TenantId"] = TENANT,
                ["EventId"] = 900,
                ["UserId"] = 3,
                ["Role"] = "Steward",
            }
        );

        var ride70 = Ride(70, 900, 1, Person(0, "Иван Петров", club: "Sofia"), Person(20, "Burya"));
        await Insert(
            database,
            "event_participations",
            ride70,
            Ride(71, 900, 2, Person(0, "IVAN PETROV"), Person(21, "Vihar")),
            Ride(
                72,
                900,
                3,
                Person(9, "Petar Dimitrov", country: Country(1, "Bulgaria", "BGR", "BUL")),
                Person(22, "Zora")
            ), // no Setup row
            Ride(73, 900, 4, Person(0, "Maria Georgieva"), Person(23, "Thunderbold")), // a Horse a letter from Thunderbolt
            Ride(74, 900, 5, Person(0, "Todor Ivanov"), Person(0, "Nightfall")),
            Ride(
                75,
                900,
                6,
                Person(6, "Nikola Todorov", fei: "400", country: Country(1, "Bulgaria", "BGR", "BUL")),
                Person(24, "Zora")
            ),
            Ride(76, 900, 7, Person(5, "Maria Georgiev"), Person(27, "Stormy")), // only the id 5 ties her to the row of Setup
            Ride(77, 900, 8, Person(0, "Nikola Todorow"), Person(28, "Hurricane")), // the spelling of the row 10, in a ride
            Ride(78, 900, 9, Person(11, "Georgi Ivanov"), Person(29, "Rainbow")),
            Ride(80, 901, 1, Person(9, "Petar Dimitrov"), Person(22, "Zora")),
            Ride(81, 901, 2, Person(0, "Yordan Ivanov", fei: "300"), Person(25, "Vihar")), // one FEI ID, two spellings
            Ride(82, 901, 3, Person(0, "Iordan Ivanov", fei: "300"), Person(26, "Burya")),
            Ride(90, 903, 1, Person(0, "Todor Ivanov"), Person(0, "Nightfall"))
        );
        await Insert(
            database,
            "event_rankings",
            Ranking(300, 900, (70, 1), (71, 2), (72, 3)), // placed
            Ranking(301, 900, (70, 1)), // the only entry is rank 1
            Ranking(302, 901, (80, 1), (81, 2), (82, 3)),
            Ranking(303, 903, (90, 0)) // Live: no placings yet
        );
        await Insert(
            database,
            "event_handouts",
            new BsonDocument
            {
                ["_id"] = 400,
                ["TenantId"] = TENANT,
                ["EventId"] = 900,
                ["ParticipationId"] = 72,
            }
        );
        await Insert(
            database,
            "configure_events",
            new BsonDocument
            {
                ["_id"] = 902,
                ["TenantId"] = TENANT,
                ["Name"] = "Winter ride",
                ["Location"] = "Sofia",
                ["Country"] = bulgaria.DeepClone(),
                ["Loops"] = new BsonArray
                {
                    new BsonDocument { ["_id"] = 40, ["Distance"] = 40.0 },
                },
                ["Officials"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["_id"] = 50,
                        ["Name"] = "Judge Example",
                        ["Role"] = "Steward",
                        ["User"] = new BsonDocument { ["_id"] = 3, ["Email"] = "official@example.test" },
                    },
                },
                ["Operators"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["_id"] = 51,
                        ["Role"] = "Steward",
                        ["User"] = new BsonDocument { ["_id"] = 3, ["Email"] = "official@example.test" },
                    },
                },
                ["Combinations"] = new BsonArray
                {
                    Combination(
                        1,
                        Person(7, "Stefan Kolev", fei: "100", country: bulgaria, club: "Sofia"),
                        Person(30, "Thunderbolt")
                    ),
                },
                ["Competitions"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["_id"] = 500,
                        ["Name"] = "CEI 1*",
                        ["Phases"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["_id"] = 60,
                                ["Recovery"] = 30,
                                ["Loop"] = new BsonDocument { ["_id"] = 40, ["Distance"] = 40.0 },
                            },
                        },
                        ["Participations"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["_id"] = 600,
                                ["Category"] = "Senior",
                                ["Combination"] = Combination(
                                    2,
                                    Person(8, "Stefan Kolev", fei: "101", country: bulgaria, club: "Sofia"),
                                    Person(31, "Zora")
                                ),
                            },
                        },
                    },
                },
            }
        );
        await Insert(
            database,
            "event_user_sessions",
            new BsonDocument
            {
                ["_id"] = ObjectId.Parse("650000000000000000000001"),
                ["Id"] = 4,
                ["TenantId"] = TENANT,
                ["EventId"] = 900,
                ["UserIdentifier"] = "someone@example.test",
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

    static async Task Insert(IMongoDatabase database, string collection, params BsonDocument[] documents)
    {
        await database.GetCollection<BsonDocument>(collection).InsertManyAsync(documents);
    }
}
