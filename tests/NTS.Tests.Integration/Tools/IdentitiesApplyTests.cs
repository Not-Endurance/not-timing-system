using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tests.Integration.Harness;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0009, ADR-0010, #637: <c>migrate-identities --apply</c> on a MongoDB in a container. The data is the cutover's: every collection
/// in the shape the code reads, with the integers of before as ids and as references. Every id becomes the Guid of its space and its
/// scope, the copies of one Athlete or one Horse take the Guid of its row, the rows that are the same person merge and the people who
/// have none are given one, and the run can be stopped at any step and run again.
/// </summary>
public sealed class IdentitiesApplyTests : IClassFixture<MongoFixture>
{
    static readonly Guid EVENT_900 = LegacyGuids.Of("Event", null, 900);
    static readonly Guid EVENT_901 = LegacyGuids.Of("Event", null, 901);
    static readonly Guid EVENT_902 = LegacyGuids.Of("Event", null, 902);

    readonly MongoFixture _mongo;

    public IdentitiesApplyTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Every_id_and_every_reference_of_every_collection_is_a_Guid_and_the_verification_passes()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(database);

        Assert.Empty(result.Refusals);
        Assert.All(result.Checks, x => Assert.True(x.Passed, $"{x.Name}: {string.Join("; ", x.Details)}"));
        Assert.True(result.Succeeded);
        var integers = new List<string>();
        foreach (var collection in LegacyWorldData.COLLECTIONS)
        {
            foreach (var document in await ReadAsync(database, collection))
            {
                IntegerIds(document, collection, "", integers);
            }
        }

        Assert.Empty(integers);
    }

    [Fact]
    public async Task The_documents_converted_made_and_merged_are_counted()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(database);

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["countries"] = 2,
                ["clubs"] = 1,
                ["users"] = 1,
                ["event_informations"] = 3,
                ["configure_events"] = 1,
                ["event_officials"] = 2,
                ["event_operators"] = 1,
                ["event_participations"] = 13,
                ["event_rankings"] = 4,
                ["event_handouts"] = 1,
                ["event_user_sessions"] = 1,
                ["athletes"] = 10, // the seven rows that stay (the eighth merges away), and the three that are made
                ["horses"] = 9, // the two rows, and the seven that are made
            },
            result.Written
        );
        Assert.Equal(10, result.RowsMade);
        Assert.Equal(1, result.RowsMerged); // the second row of Maria Georgieva
    }

    [Fact]
    public async Task Every_document_is_read_by_the_model_of_its_collection_once_it_is_converted_and_none_was_before()
    {
        ApiMongo.Configure();
        var database = await SeededAsync();
        var readers = new (string Collection, Type Model)[]
        {
            ("athletes", typeof(NTS.Contracts.Setup.Models.AthleteModel)),
            ("horses", typeof(NTS.Contracts.Setup.Models.HorseModel)),
            ("clubs", typeof(NTS.Contracts.Setup.Models.ClubModel)),
            ("countries", typeof(NTS.Contracts.Shared.Models.CountryModel)),
            ("users", typeof(NTS.Contracts.Setup.Models.UserModel)),
            ("configure_events", typeof(NTS.Contracts.Setup.Models.ConfigureEventModel)),
            ("event_informations", typeof(NTS.Contracts.Core.Models.EventInformationModel)),
            ("event_officials", typeof(NTS.Contracts.Core.Models.OfficialModel)),
            ("event_operators", typeof(NTS.Contracts.Core.Models.OperatorModel)),
            ("event_participations", typeof(NTS.Contracts.Core.Models.ParticipationModel)),
            ("event_rankings", typeof(NTS.Contracts.Core.Models.RankingModel)),
            ("event_handouts", typeof(NTS.Contracts.Core.Models.HandoutModel)),
            ("event_user_sessions", typeof(NTS.Contracts.Watcher.Models.NtsUserSessionModel)),
        };
        var before = new List<string>();
        foreach (var (collection, model) in readers)
        {
            foreach (var document in await ReadAsync(database, collection))
            {
                try
                {
                    BsonSerializer.Deserialize(document, model);
                }
                catch (FormatException)
                {
                    before.Add(collection);
                    break;
                }
            }
        }

        await ApplyAsync(database);

        var unreadable = new List<string>();
        foreach (var (collection, model) in readers)
        {
            foreach (var document in await ReadAsync(database, collection))
            {
                try
                {
                    BsonSerializer.Deserialize(document, model);
                }
                catch (FormatException error)
                {
                    unreadable.Add($"{collection}: {error.Message}");
                }
            }
        }

        Assert.True(unreadable.Count == 0, string.Join(Environment.NewLine, unreadable));
        Assert.Contains("event_handouts", before); // the integers of before are not what the models read
        Assert.Contains("event_participations", before);
    }

    [Fact]
    public async Task The_Events_and_everything_they_keep_take_the_Guids_of_their_spaces_and_scopes()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var information = await FindAsync(database, "event_informations", EVENT_900);
        Assert.Equal("Spring ride", information["Name"].AsString);
        Assert.Equal(LegacyGuids.Of("Country", null, 1), information["Country"]["_id"].AsGuid);
        var ride = await FindAsync(database, "event_participations", LegacyGuids.Of("Participation", EVENT_900, 70));
        Assert.Equal(EVENT_900, ride["EventId"].AsGuid);
        Assert.Equal(LegacyGuids.Of("Combination", EVENT_900, 1001), ride["Combination"]["_id"].AsGuid);
        Assert.Equal(
            LegacyGuids.Of("Phase", LegacyGuids.Of("Participation", EVENT_900, 70), 5),
            ride["Phases"][0]["_id"].AsGuid
        );
        Assert.Equal(Guid.Parse("00000000-0000-4000-8000-000000000011"), ride["Phases"][0]["Events"][0]["_id"].AsGuid); // a time event was a Guid when it was made
        var ranking = await FindAsync(database, "event_rankings", LegacyGuids.Of("Ranking", EVENT_900, 300));
        Assert.Equal(EVENT_900, ranking["EventId"].AsGuid);
        Assert.Equal(
            new[] { 70, 71, 72 }.Select(x => LegacyGuids.Of("Participation", EVENT_900, x)),
            ranking["Entries"].AsBsonArray.Select(x => x["ParticipationId"].AsGuid)
        );
        Assert.Equal(new[] { 1, 2, 3 }, ranking["Entries"].AsBsonArray.Select(x => x["Rank"].AsInt32));
        var handout = await FindAsync(database, "event_handouts", LegacyGuids.Of("Handout", EVENT_900, 400));
        Assert.Equal(LegacyGuids.Of("Participation", EVENT_900, 72), handout["ParticipationId"].AsGuid);
        var official = await FindAsync(database, "event_officials", LegacyGuids.Of("Official", EVENT_900, 12));
        Assert.Equal(LegacyGuids.Of("User", null, 3), official["UserId"].AsGuid);
        Assert.False(
            (await FindAsync(database, "event_officials", LegacyGuids.Of("Official", EVENT_900, 14))).Contains("UserId")
        );
        var operatorRow = await FindAsync(database, "event_operators", LegacyGuids.Of("Operator", EVENT_900, 13));
        Assert.Equal(LegacyGuids.Of("User", null, 3), operatorRow["UserId"].AsGuid);
        var athleteOfARide = ride["Combination"]["Athlete"];
        Assert.Equal(LegacyGuids.Of("Club", null, 7), athleteOfARide["Club"]["_id"].AsGuid);
        var withCountry = await FindAsync(
            database,
            "event_participations",
            LegacyGuids.Of("Participation", EVENT_900, 75)
        );
        Assert.Equal(
            LegacyGuids.Of("Country", null, 1),
            withCountry["Combination"]["Athlete"]["Country"]["_id"].AsGuid
        );
        var user = await FindAsync(database, "users", LegacyGuids.Of("User", null, 3));
        Assert.Equal("official@example.test", user["Email"].AsString);
        var club = await FindAsync(database, "clubs", LegacyGuids.Of("Club", null, 7));
        Assert.Equal("Sofia", club["Name"].AsString);
        var country = await FindAsync(database, "countries", LegacyGuids.Of("Country", null, 1));
        Assert.Equal("BUL", country["NfCode"].AsString);
        var session = (await ReadAsync(database, "event_user_sessions")).Single();
        Assert.Equal(ObjectId.Parse("650000000000000000000001"), session["_id"].AsObjectId);
        Assert.Equal(LegacyGuids.Of("UserSession", EVENT_900, 4), session["Id"].AsGuid);
        Assert.Equal(EVENT_900, session["EventId"].AsGuid);
    }

    [Fact]
    public async Task A_Setup_is_scoped_to_its_Event_and_shares_its_Combinations_and_its_people_with_the_Core_copies()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var setup = await FindAsync(database, "configure_events", EVENT_902);
        Assert.Equal(LegacyGuids.Of("Loop", EVENT_902, 40), setup["Loops"][0]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("SetupOfficial", EVENT_902, 50), setup["Officials"][0]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("User", null, 3), setup["Officials"][0]["User"]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("SetupOperator", EVENT_902, 51), setup["Operators"][0]["_id"].AsGuid);
        var competition = setup["Competitions"][0];
        Assert.Equal(LegacyGuids.Of("Competition", EVENT_902, 500), competition["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("SetupPhase", EVENT_902, 60), competition["Phases"][0]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("Loop", EVENT_902, 40), competition["Phases"][0]["Loop"]["_id"].AsGuid);
        Assert.Equal(
            LegacyGuids.Of("SetupParticipation", EVENT_902, 600),
            competition["Participations"][0]["_id"].AsGuid
        );
        Assert.Equal(LegacyGuids.Of("Combination", EVENT_902, 1001), setup["Combinations"][0]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("Athlete", null, 7), setup["Combinations"][0]["Athlete"]["_id"].AsGuid); // the Athlete of the pool is the row of Setup
        Assert.Equal(LegacyGuids.Of("Country", null, 1), setup["Country"]["_id"].AsGuid);
        var inThePool = setup["Combinations"][0]["Athlete"];
        Assert.Equal(LegacyGuids.Of("Country", null, 1), inThePool["Country"]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("Club", null, 7), inThePool["Club"]["_id"].AsGuid);
        var inTheCompetition = setup["Competitions"][0]["Participations"][0]["Combination"]["Athlete"];
        Assert.Equal(LegacyGuids.Of("Country", null, 1), inTheCompetition["Country"]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("Club", null, 7), inTheCompetition["Club"]["_id"].AsGuid);
        Assert.Equal(
            LegacyGuids.Of("Athlete", null, 8),
            competition["Participations"][0]["Combination"]["Athlete"]["_id"].AsGuid
        );
    }

    [Fact]
    public async Task A_person_in_several_spellings_and_Events_ends_with_one_Guid_and_the_rows_of_the_same_person_merge_into_one()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var ivan = LegacyGuids.Of("Athlete", null, 1);
        var rows = await ReadAsync(database, "athletes");
        var row = Assert.Single(rows, x => x["_id"].AsGuid == ivan);
        Assert.Equal("Иван Петров", row["Name"].AsString);
        Assert.Equal("Ivan Petrov", row["NameEnglish"].AsString);
        Assert.Equal(LegacyGuids.Of("Country", null, 1), row["Country"]["_id"].AsGuid);
        Assert.Equal(LegacyGuids.Of("Club", null, 7), row["Club"]["_id"].AsGuid);
        foreach (var number in new[] { 70, 71 })
        {
            var ride = await FindAsync(
                database,
                "event_participations",
                LegacyGuids.Of("Participation", EVENT_900, number)
            );
            Assert.Equal(ivan, ride["Combination"]["Athlete"]["_id"].AsGuid);
        }

        var maria = LegacyGuids.Of("Athlete", null, 5); // the row a Participation rides on, and not the one with the lower id
        Assert.Single(rows, x => x["Name"].AsString == "Maria Georgieva"); // the duplicate row is gone
        Assert.Equal(maria, Assert.Single(rows, x => x["Name"].AsString == "Maria Georgieva")["_id"].AsGuid);
        foreach (var number in new[] { 73, 76 }) // the second is a spelling of hers that only her id ties to the row
        {
            var ride = await FindAsync(
                database,
                "event_participations",
                LegacyGuids.Of("Participation", EVENT_900, number)
            );
            Assert.Equal(maria, ride["Combination"]["Athlete"]["_id"].AsGuid);
        }
        var burya = LegacyGuids.Of("Horse", null, 20);
        foreach (var (number, eventId) in new[] { (70, EVENT_900), (82, EVENT_901) })
        {
            var ride = await FindAsync(
                database,
                "event_participations",
                LegacyGuids.Of("Participation", eventId, number)
            );
            Assert.Equal(burya, ride["Combination"]["Horse"]["_id"].AsGuid);
        }
    }

    [Fact]
    public async Task What_is_left_to_review_stays_separate_identities_each_with_its_own_Guid()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var rows = await ReadAsync(database, "athletes");
        Assert.Contains(rows, x => x["_id"].AsGuid == LegacyGuids.Of("Athlete", null, 7)); // Stefan Kolev, FEI ID 100
        Assert.Contains(rows, x => x["_id"].AsGuid == LegacyGuids.Of("Athlete", null, 8)); // and the one of FEI ID 101
        Assert.Contains(rows, x => x["_id"].AsGuid == LegacyGuids.Of("Athlete", null, 10)); // Nikola Todorow
        Assert.Contains(rows, x => x["_id"].AsGuid == LegacyGuids.Of("Athlete", null, 6)); // Nikola Todorov
        var horses = await ReadAsync(database, "horses");
        Assert.Contains(horses, x => x["_id"].AsGuid == LegacyGuids.Of("Horse", null, 30)); // Thunderbolt
        Assert.Contains(horses, x => x["_id"].AsGuid == LegacyGuids.Of("Horse", null, 23)); // and Thunderbold, a letter from it
    }

    [Fact]
    public async Task An_identity_with_no_row_gets_one_built_from_its_latest_copy_with_the_Guid_of_its_lowest_id()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var rows = await ReadAsync(database, "athletes");
        var petar = Assert.Single(rows, x => x["_id"].AsGuid == LegacyGuids.Of("Athlete", null, 9));
        Assert.Equal("Petar Dimitrov", petar["Name"].AsString);
        Assert.Equal(LegacyWorldData.TENANT, petar["TenantId"].AsString);
        Assert.Equal(LegacyGuids.Of("Country", null, 1), petar["Country"]["_id"].AsGuid); // the ISO country of the countries
        Assert.Equal("BG", petar["Country"]["IsoCode"].AsString); // and not the BGR that the ride was entered with
        foreach (var (number, eventId) in new[] { (72, EVENT_900), (80, EVENT_901) })
        {
            var ride = await FindAsync(
                database,
                "event_participations",
                LegacyGuids.Of("Participation", eventId, number)
            );
            Assert.Equal(petar["_id"].AsGuid, ride["Combination"]["Athlete"]["_id"].AsGuid);
        }

        var todor = Assert.Single(rows, x => x["Name"].AsString == "Todor Ivanov");
        Assert.Equal(LegacyGuids.Of("Athlete", null, "name:todor ivanov"), todor["_id"].AsGuid); // no id and no FEI ID, in two Events
        var ivanov = Assert.Single(rows, x => x.TryGetValue("FeiId", out var fei) && fei == "300");
        Assert.Equal(LegacyGuids.Of("Athlete", null, "fei:300"), ivanov["_id"].AsGuid); // one FEI ID in two spellings
        Assert.False(ivanov.Contains("NameEnglish")); // and the other spelling is Latin too
        var horses = await ReadAsync(database, "horses");
        var zora = Assert.Single(horses, x => x["Name"].AsString == "Zora");
        Assert.Equal(LegacyGuids.Of("Horse", null, 22), zora["_id"].AsGuid);
    }

    [Fact]
    public async Task The_User_an_Athlete_once_stored_stays_as_it_is_and_is_no_trouble()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(database);

        Assert.True(result.Succeeded, string.Join("; ", result.Refusals));
        var row = await FindAsync(database, "athletes", LegacyGuids.Of("Athlete", null, 7));
        Assert.Equal(3, row["User"]["_id"].AsInt32);
        Assert.Equal("official@example.test", row["User"]["Email"].AsString);
    }

    [Fact]
    public async Task A_row_in_Cyrillic_with_no_English_name_is_given_the_Latin_spelling_of_a_copy_and_one_that_has_it_is_not()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var georgi = await FindAsync(database, "athletes", LegacyGuids.Of("Athlete", null, 11));
        Assert.Equal("Георги Иванов", georgi["Name"].AsString);
        Assert.Equal("Georgi Ivanov", georgi["NameEnglish"].AsString);
        var ivan = await FindAsync(database, "athletes", LegacyGuids.Of("Athlete", null, 1));
        Assert.Equal("Ivan Petrov", ivan["NameEnglish"].AsString);
        var stefan = await FindAsync(database, "athletes", LegacyGuids.Of("Athlete", null, 7));
        Assert.False(stefan.Contains("NameEnglish")); // a Latin name is its own Latin spelling
    }

    [Fact]
    public async Task An_English_name_that_someone_wrote_while_the_run_was_stopped_is_never_written_over()
    {
        var database = await SeededAsync();
        var steps = 0;
        await Assert.ThrowsAsync<StoppedException>(
            () =>
                ApplyAsync(
                    database,
                    _ =>
                    {
                        if (++steps == 1) // the plan is kept, and nothing else is written
                        {
                            throw new StoppedException();
                        }
                    }
                )
        );
        await Collection(database, "athletes")
            .UpdateOneAsync(
                new BsonDocument("_id", 11),
                new BsonDocument("$set", new BsonDocument("NameEnglish", "G. Ivanov"))
            );

        var result = await ApplyAsync(database);

        Assert.True(result.Succeeded, string.Join("; ", result.Refusals));
        var georgi = await FindAsync(database, "athletes", LegacyGuids.Of("Athlete", null, 11));
        Assert.Equal("G. Ivanov", georgi["NameEnglish"].AsString);
    }

    [Theory]
    [InlineData("event_officials", 12, "UserId")]
    [InlineData("event_operators", 13, "UserId")]
    [InlineData("event_officials", 12, "EventId")]
    [InlineData("event_operators", 13, "EventId")]
    [InlineData("event_participations", 70, "EventId")]
    [InlineData("event_rankings", 300, "EventId")]
    [InlineData("event_rankings", 300, "Entries.0.ParticipationId")]
    [InlineData("event_handouts", 400, "EventId")]
    [InlineData("event_user_sessions", 0, "EventId")]
    [InlineData("configure_events", 902, "Officials.0.User._id")]
    [InlineData("configure_events", 902, "Operators.0.User._id")]
    public async Task Every_kind_of_reference_that_names_something_that_is_not_there_fails_the_verification(
        string collection,
        int id,
        string field
    )
    {
        var database = await SeededAsync();
        var filter = collection == "event_user_sessions" ? new BsonDocument() : new BsonDocument("_id", id);
        await Collection(database, collection)
            .UpdateOneAsync(filter, new BsonDocument("$set", new BsonDocument(field, 7777)));

        var result = await ApplyAsync(database);

        Assert.Empty(result.Refusals);
        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Every reference resolves", failed.Name);
        Assert.Contains(failed.Details, x => x.StartsWith(collection, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("event_officials", "UserId")]
    [InlineData("event_operators", "UserId")]
    [InlineData("event_officials", "EventId")]
    [InlineData("event_operators", "EventId")]
    [InlineData("event_participations", "EventId")]
    [InlineData("event_rankings", "EventId")]
    [InlineData("event_rankings", "Entries.0.ParticipationId")]
    [InlineData("event_handouts", "EventId")]
    [InlineData("event_handouts", "ParticipationId")]
    [InlineData("event_user_sessions", "EventId")]
    [InlineData("configure_events", "Officials.0.User._id")]
    [InlineData("configure_events", "Operators.0.User._id")]
    public async Task A_reference_that_is_broken_after_the_conversion_is_found_by_the_verification_whatever_the_collection(
        string collection,
        string field
    )
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings")
                {
                    Collection(database, collection)
                        .UpdateOne(
                            new BsonDocument(),
                            new BsonDocument(
                                "$set",
                                new BsonDocument(field, new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard))
                            )
                        );
                }
            }
        );

        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Every reference resolves", failed.Name);
        Assert.Contains(
            failed.Details,
            x => x.StartsWith(collection, StringComparison.Ordinal) && x.Contains(field.Split('.')[^1])
        );
    }

    [Theory]
    [InlineData("event_participations", "Combination.Athlete.Name")]
    [InlineData("event_participations", "Combination.Athlete.NameEnglish")]
    [InlineData("event_officials", "Name")]
    [InlineData("event_informations", "Name")]
    [InlineData("event_rankings", "Name")]
    public async Task A_name_that_changes_in_Core_while_the_run_goes_on_is_found_by_the_verification(
        string collection,
        string field
    )
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings")
                {
                    Collection(database, collection)
                        .UpdateOne(
                            new BsonDocument(),
                            new BsonDocument("$set", new BsonDocument(field, "Someone Else"))
                        );
                }
            }
        );

        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Names in Core are unchanged", failed.Name);
    }

    [Theory]
    [InlineData("event_informations")]
    [InlineData("event_participations")]
    [InlineData("event_rankings")]
    [InlineData("event_handouts")]
    public async Task A_document_that_goes_missing_in_any_of_the_four_collections_fails_the_count(string collection)
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings")
                {
                    Collection(database, collection).DeleteOne(new BsonDocument());
                }
            }
        );

        var counts = Assert.Single(result.Checks, x => x.Name.StartsWith("The counts", StringComparison.Ordinal));
        Assert.False(counts.Passed);
        Assert.Contains(counts.Details, x => x.StartsWith(collection + " had ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("event_handouts", "ParticipationId", 70)] // an integer that is converted, as the apply would have done
    [InlineData("event_participations", "Phases.0.Events.0._id", 5)] // an integer where a Guid is made
    [InlineData("event_participations", "BrokenId", 3)] // an integer the table does not know
    [InlineData("event_handouts", "ParticipationId", "seventy")] // an id that is a text
    public async Task An_id_that_is_not_a_Guid_after_the_conversion_fails_the_first_check_and_no_other(
        string collection,
        string field,
        object value
    )
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings")
                {
                    var bson = value is int number ? new BsonInt32(number) : (BsonValue)value.ToString()!;
                    Collection(database, collection)
                        .UpdateOne(new BsonDocument(), new BsonDocument("$set", new BsonDocument(field, bson)));
                }
            }
        );

        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("No integer id or reference remains", failed.Name);
        Assert.StartsWith(collection, Assert.Single(failed.Details), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_document_that_is_there_as_its_original_and_as_its_twin_is_one_document_for_the_counts()
    {
        // a run that stopped between the write of a twin and the delete of the original: the next run finds both
        var database = await SeededAsync();
        var participations = Collection(database, "event_participations");
        var twin = (await participations.Find(new BsonDocument("_id", 70)).SingleAsync()).DeepClone().AsBsonDocument;
        twin["_id"] = new BsonBinaryData(LegacyGuids.Of("Participation", EVENT_900, 70), GuidRepresentation.Standard);
        await participations.InsertOneAsync(twin);

        var result = await ApplyAsync(database);

        Assert.True(result.Succeeded, string.Join("; ", result.Checks.SelectMany(x => x.Details)));
        Assert.Equal(13, await participations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task A_row_the_run_made_and_someone_corrected_while_it_was_stopped_is_not_made_again()
    {
        var database = await SeededAsync();
        await Assert.ThrowsAsync<StoppedException>(
            () =>
                ApplyAsync(
                    database,
                    step =>
                    {
                        if (step == "athletes") // the rows of the people who had none are made
                        {
                            throw new StoppedException();
                        }
                    }
                )
        );
        var petar = LegacyGuids.Of("Athlete", null, 9);
        await Collection(database, "athletes")
            .UpdateOneAsync(
                new BsonDocument("_id", new BsonBinaryData(petar, GuidRepresentation.Standard)),
                new BsonDocument("$set", new BsonDocument("Name", "Petar Dimitrov (corrected)"))
            );

        var result = await ApplyAsync(database);

        Assert.True(result.Succeeded, string.Join("; ", result.Refusals));
        Assert.Equal("Petar Dimitrov (corrected)", (await FindAsync(database, "athletes", petar))["Name"].AsString);
        Assert.Equal(7, result.RowsMade); // the rows of the Horses, which the run that stopped had not reached
    }

    [Fact]
    public async Task What_every_Core_document_says_of_a_person_is_as_it_was_written()
    {
        var database = await SeededAsync();
        var before = await NamesAsync(database);

        await ApplyAsync(database);

        Assert.Equal(before, await NamesAsync(database));
        var ride = await FindAsync(database, "event_participations", LegacyGuids.Of("Participation", EVENT_900, 71));
        Assert.Equal("IVAN PETROV", ride["Combination"]["Athlete"]["Name"].AsString); // not the row's name
    }

    [Fact]
    public async Task The_steps_are_done_in_this_order()
    {
        var database = await SeededAsync();
        var steps = new List<string>();

        await ApplyAsync(database, step => steps.Add(step));

        Assert.Equal(
            [
                "plan",
                "countries",
                "clubs",
                "users",
                "event_informations",
                "configure_events",
                "event_officials",
                "event_operators",
                "event_participations",
                "event_rankings",
                "event_handouts",
                "event_user_sessions",
                "athletes",
                "horses",
                "merged-rows",
                "plan-consumed",
                "rankings",
            ],
            steps
        );
    }

    [Fact]
    public async Task Reading_and_writing_in_pages_of_three_documents_gives_the_state_that_pages_of_a_hundred_do()
    {
        var whole = await SeededAsync();
        await ApplyAsync(whole);
        var paged = await SeededAsync();

        var result = await ApplyAsync(paged, pageSize: 3);

        Assert.True(result.Succeeded, string.Join("; ", result.Refusals));
        AssertSame(await LegacyWorldData.SnapshotAsync(whole), await LegacyWorldData.SnapshotAsync(paged));
    }

    [Fact]
    public async Task A_collection_that_appears_during_the_run_with_a_number_for_an_id_is_found_by_the_verification()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings")
                {
                    Collection(database, "settings").InsertOne(new BsonDocument { ["_id"] = 1 });
                }
            }
        );

        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("No integer id or reference remains", failed.Name);
        Assert.Contains(failed.Details, x => x.StartsWith("settings:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task What_does_not_hold_is_listed_in_part_and_the_rest_is_counted()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings") // eleven of the thirteen Participations name an Event that is not there
                {
                    Collection(database, "event_participations")
                        .UpdateMany(
                            new BsonDocument("Combination.Number", new BsonDocument("$gte", 1)),
                            new BsonDocument(
                                "$set",
                                new BsonDocument(
                                    "EventId",
                                    new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard)
                                )
                            )
                        );
                }
            }
        );

        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Every reference resolves", failed.Name);
        Assert.Equal(9, failed.Details.Count); // eight listed, and the count of the rest
        Assert.Equal("... and 5 more", failed.Details[^1]);
    }

    [Fact]
    public async Task Running_apply_a_second_time_changes_nothing()
    {
        var database = await SeededAsync();
        await ApplyAsync(database);
        var once = await LegacyWorldData.SnapshotAsync(database);

        var again = await ApplyAsync(database);

        Assert.Empty(again.Refusals);
        Assert.True(again.Succeeded);
        Assert.Empty(again.Written);
        Assert.Equal(0, again.RowsMade);
        Assert.Equal(0, again.RowsMerged);
        AssertSame(once, await LegacyWorldData.SnapshotAsync(database));
    }

    [Fact]
    public async Task A_run_stopped_after_any_step_and_run_again_ends_in_the_state_of_a_run_that_was_not_stopped()
    {
        var whole = await SeededAsync();
        var steps = new List<string>();
        await ApplyAsync(whole, step => steps.Add(step));
        var expected = await LegacyWorldData.SnapshotAsync(whole);
        Assert.True(steps.Count > 10, string.Join(", ", steps));

        for (var stop = 1; stop <= steps.Count; stop++)
        {
            var database = await SeededAsync();
            var seen = 0;
            await Assert.ThrowsAsync<StoppedException>(
                () =>
                    ApplyAsync(
                        database,
                        _ =>
                        {
                            if (++seen == stop)
                            {
                                throw new StoppedException();
                            }
                        }
                    )
            );

            var resumed = await ApplyAsync(database);

            Assert.True(
                resumed.Succeeded,
                $"after step {stop} ({steps[stop - 1]}): {string.Join("; ", resumed.Refusals)}"
            );
            AssertSame(
                expected,
                await LegacyWorldData.SnapshotAsync(database),
                $"after step {stop} ({steps[stop - 1]})"
            );
        }
    }

    [Fact]
    public async Task A_decision_that_joins_two_Athletes_after_each_has_a_Guid_stops_the_run_and_says_so()
    {
        var database = await SeededAsync();
        await ApplyAsync(database); // Stefan Kolev of FEI ID 100 and the one of 101 stay two
        var before = await LegacyWorldData.SnapshotAsync(database);

        var result = await ApplyAsync(
            database,
            decisions: IdentityDecisions.Parse(
                """{ "items": { "athlete (fei 100): stefan kolev <> athlete (fei 101): stefan kolev": "same" } }"""
            )
        );

        Assert.False(result.Succeeded);
        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("two Guids", refusal);
        Assert.Contains("Stefan Kolev", refusal);
        AssertSame(before, await LegacyWorldData.SnapshotAsync(database));
    }

    [Fact]
    public async Task A_decision_that_keeps_two_Athletes_apart_after_they_were_made_one_stops_the_run_and_says_so()
    {
        var database = await SeededAsync();
        const string NIKOLA = "athlete (fei 400): nikola todorov <> athlete: nikola todorow";
        await ApplyAsync(database, decisions: IdentityDecisions.Parse($$"""{ "items": { "{{NIKOLA}}": "same" } }"""));
        var before = await LegacyWorldData.SnapshotAsync(database);
        Assert.DoesNotContain(await ReadAsync(database, "athletes"), x => x["Name"].AsString == "Nikola Todorow"); // the row of the variant was merged into the row of Nikola Todorov

        var result = await ApplyAsync(
            database,
            decisions: IdentityDecisions.Parse($$"""{ "items": { "{{NIKOLA}}": "different" } }""")
        );

        Assert.False(result.Succeeded);
        Assert.Contains(result.Refusals, x => x.Contains(NIKOLA) && x.Contains("different"));
        AssertSame(before, await LegacyWorldData.SnapshotAsync(database));
    }

    [Fact]
    public async Task A_decision_to_join_what_the_owner_reviewed_merges_the_rows_and_re_points_the_copies()
    {
        var database = await SeededAsync();
        const string NIKOLA = "athlete (fei 400): nikola todorov <> athlete: nikola todorow";

        var result = await ApplyAsync(
            database,
            decisions: IdentityDecisions.Parse($$"""{ "items": { "{{NIKOLA}}": "same" } }""")
        );

        Assert.True(result.Succeeded, string.Join("; ", result.Refusals));
        var rows = await ReadAsync(database, "athletes");
        var row = Assert.Single(rows, x => x["Name"].AsString.StartsWith("Nikola Todor", StringComparison.Ordinal));
        Assert.Equal("Nikola Todorov", row["Name"].AsString); // the row with a copy riding on it, and the one with the FEI ID
        Assert.Equal(LegacyGuids.Of("Athlete", null, 6), row["_id"].AsGuid);
        Assert.Equal(2, result.RowsMerged); // the row of the variant, and the second row of Maria Georgieva
    }

    [Theory]
    [InlineData("a copy with no name")]
    [InlineData("a decision that matches nothing")]
    [InlineData("an id that is neither an integer nor a Guid")]
    [InlineData("an integer where a Guid is made")]
    [InlineData("a Ranking that still holds the copy of a Participation")]
    [InlineData("an integer for the actor of a time event")]
    [InlineData("an integer for the group of a Snapshot")]
    [InlineData("a reference to an Event that is a text")]
    [InlineData("a collection the table does not know")]
    [InlineData("a collection with a long for an id")]
    [InlineData("a collection with a double for an id")]
    [InlineData("a collection with a decimal for an id")]
    public async Task What_the_data_and_the_decisions_do_not_agree_on_or_the_table_cannot_convert_refuses_the_run_and_writes_nothing(
        string trouble
    )
    {
        var database = await SeededAsync();
        var decisions = IdentityDecisions.None;
        switch (trouble)
        {
            case "a copy with no name":
                await Collection(database, "event_participations")
                    .UpdateOneAsync(
                        new BsonDocument("_id", 70),
                        new BsonDocument("$unset", new BsonDocument("Combination.Athlete.Name", ""))
                    );
                break;
            case "a decision that matches nothing":
                decisions = IdentityDecisions.Parse("""{ "items": { "athlete: nobody <> athlete: noone": "same" } }""");
                break;
            case "an id that is neither an integer nor a Guid":
                await Collection(database, "event_handouts")
                    .UpdateOneAsync(
                        new BsonDocument("_id", 400),
                        new BsonDocument("$set", new BsonDocument("ParticipationId", "seventy-two"))
                    );
                break;
            case "an integer where a Guid is made":
                await Collection(database, "event_participations")
                    .UpdateOneAsync(
                        new BsonDocument("_id", 70),
                        new BsonDocument("$set", new BsonDocument("Phases.0.Events.0._id", 17))
                    );
                break;
            case "a Ranking that still holds the copy of a Participation":
                await Collection(database, "event_rankings")
                    .UpdateOneAsync(
                        new BsonDocument("_id", 300),
                        new BsonDocument(
                            "$set",
                            new BsonDocument(
                                "Entries.0.Participation",
                                new BsonDocument
                                {
                                    ["_id"] = 70,
                                    ["Combination"] = LegacyWorldData.Combination(
                                        1,
                                        LegacyWorldData.Person(0, "Иван Петров"),
                                        LegacyWorldData.Person(20, "Burya")
                                    ),
                                }
                            )
                        )
                    );
                break;
            case "an integer for the actor of a time event":
                await Collection(database, "event_participations")
                    .UpdateOneAsync(
                        new BsonDocument("_id", 70),
                        new BsonDocument("$set", new BsonDocument("Phases.0.Events.0.ActorId", 5))
                    );
                break;
            case "an integer for the group of a Snapshot":
                await Collection(database, "event_user_sessions")
                    .UpdateOneAsync(
                        new BsonDocument(),
                        new BsonDocument(
                            "$set",
                            new BsonDocument(
                                "State",
                                new BsonDocument("SnapshotHistory", new BsonArray { new BsonDocument("GroupId", 5) })
                            )
                        )
                    );
                break;
            case "a reference to an Event that is a text":
                await Collection(database, "event_officials")
                    .UpdateOneAsync(
                        new BsonDocument("_id", 12),
                        new BsonDocument("$set", new BsonDocument("EventId", "nine hundred"))
                    );
                break;
            case "a collection with a long for an id":
                await Collection(database, "settings").InsertOneAsync(new BsonDocument { ["_id"] = 1L });
                break;
            case "a collection with a double for an id":
                await Collection(database, "settings").InsertOneAsync(new BsonDocument { ["_id"] = 2.5 });
                break;
            case "a collection with a decimal for an id":
                await Collection(database, "settings")
                    .InsertOneAsync(new BsonDocument { ["_id"] = new BsonDecimal128(3m) });
                break;
            default:
                await Collection(database, "settings")
                    .InsertOneAsync(new BsonDocument { ["_id"] = 1, ["AccountId"] = "x" });
                break;
        }

        var before = await LegacyWorldData.SnapshotAsync(database);

        var result = await ApplyAsync(database, decisions: decisions);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Refusals);
        Assert.Empty(result.Steps);
        AssertSame(before, await LegacyWorldData.SnapshotAsync(database));
    }

    [Fact]
    public async Task A_run_that_stopped_is_not_finished_with_other_decisions_than_it_was_made_with()
    {
        var database = await SeededAsync();
        var steps = 0;
        await Assert.ThrowsAsync<StoppedException>(
            () =>
                ApplyAsync(
                    database,
                    _ =>
                    {
                        if (++steps == 3)
                        {
                            throw new StoppedException();
                        }
                    }
                )
        );

        var result = await ApplyAsync(
            database,
            decisions: IdentityDecisions.Parse(
                """{ "items": { "athlete (fei 100): stefan kolev <> athlete (fei 101): stefan kolev": "different" } }"""
            )
        );

        Assert.False(result.Succeeded);
        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("other decisions", refusal);
        var finished = await ApplyAsync(database);
        Assert.True(finished.Succeeded);
        Assert.True(finished.Resumed);
    }

    [Fact]
    public async Task A_run_leaves_nothing_of_itself_in_the_database_when_it_is_done()
    {
        var database = await SeededAsync();

        await ApplyAsync(database);

        var collections = await (await database.ListCollectionNamesAsync()).ToListAsync();
        Assert.DoesNotContain("migrate_identities_plan", collections);
    }

    [Fact]
    public async Task A_reference_to_something_that_is_not_there_fails_the_verification_and_the_other_checks_still_pass()
    {
        var database = await SeededAsync();
        await Collection(database, "event_handouts")
            .UpdateOneAsync(
                new BsonDocument("_id", 400),
                new BsonDocument("$set", new BsonDocument("ParticipationId", 9999))
            );

        var result = await ApplyAsync(database);

        Assert.Empty(result.Refusals);
        Assert.False(result.Succeeded);
        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Every reference resolves", failed.Name);
        Assert.Contains(failed.Details, x => x.Contains("event_handouts") && x.Contains("ParticipationId"));
        Assert.Equal(5, result.Checks.Count(x => x.Passed));
    }

    [Fact]
    public async Task A_Participation_that_goes_missing_fails_the_count_and_a_name_that_changes_fails_the_names()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings")
                {
                    Collection(database, "event_participations")
                        .DeleteOne(
                            new BsonDocument(
                                "_id",
                                new BsonBinaryData(
                                    LegacyGuids.Of("Participation", EVENT_901, 82),
                                    GuidRepresentation.Standard
                                )
                            )
                        );
                    Collection(database, "event_participations")
                        .UpdateOne(
                            new BsonDocument(
                                "_id",
                                new BsonBinaryData(
                                    LegacyGuids.Of("Participation", EVENT_900, 70),
                                    GuidRepresentation.Standard
                                )
                            ),
                            new BsonDocument("$set", new BsonDocument("Combination.Athlete.Name", "Someone Else"))
                        );
                }
            }
        );

        Assert.False(result.Succeeded);
        var failed = result.Checks.Where(x => !x.Passed).Select(x => x.Name).Order(StringComparer.Ordinal).ToList();
        Assert.Contains("The counts of Events, Participations, Rankings and Handouts are unchanged", failed);
        Assert.Contains("Names in Core are unchanged", failed);
        var counts = result.Checks.Single(x => x.Name.StartsWith("The counts", StringComparison.Ordinal));
        Assert.Contains("event_participations had 13 documents and has 12", counts.Details);
    }

    [Fact]
    public async Task A_person_who_is_not_a_Setup_row_fails_the_verification()
    {
        var database = await SeededAsync();

        var result = await ApplyAsync(
            database,
            step =>
            {
                if (step == "rankings") // the last step before the verification
                {
                    Collection(database, "event_participations")
                        .UpdateOne(
                            new BsonDocument(
                                "_id",
                                new BsonBinaryData(
                                    LegacyGuids.Of("Participation", EVENT_900, 70),
                                    GuidRepresentation.Standard
                                )
                            ),
                            new BsonDocument(
                                "$set",
                                new BsonDocument(
                                    "Combination.Athlete._id",
                                    new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard)
                                )
                            )
                        );
                }
            }
        );

        Assert.False(result.Succeeded);
        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Every Athlete and Horse id is a Setup row's", failed.Name);
    }

    /// <summary>The integers where an id or a reference is: any element called Id or ending in Id, and the _id of a document.</summary>
    static void IntegerIds(BsonValue value, string collection, string path, List<string> found)
    {
        switch (value)
        {
            case BsonDocument document:
                foreach (var element in document.Elements)
                {
                    var name = element.Name;
                    var child = path.Length == 0 ? name : $"{path}.{name}";
                    if (name == "User" && collection == "athletes")
                    {
                        continue; // what an Athlete once stored of its User stays as it is
                    }

                    if (
                        (name is "_id" or "Id" || name.EndsWith("Id", StringComparison.Ordinal))
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

    /// <summary>Every name the Core documents say, by collection and place and not by id, so that a run can be told to have changed none.</summary>
    static async Task<SortedDictionary<string, string[]>> NamesAsync(IMongoDatabase database)
    {
        var names = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (
            var collection in new[]
            {
                "event_participations",
                "event_officials",
                "event_informations",
                "event_rankings",
            }
        )
        {
            var documents = new List<string>();
            foreach (var document in await ReadAsync(database, collection))
            {
                var found = new List<string>();
                NamesOf(document, "", found);
                documents.Add(string.Join("|", found));
            }

            names[collection] = [.. documents.Order(StringComparer.Ordinal)];
        }

        return names;
    }

    static void NamesOf(BsonValue value, string path, List<string> found)
    {
        switch (value)
        {
            case BsonDocument document:
                foreach (var element in document.Elements)
                {
                    var child = path.Length == 0 ? element.Name : $"{path}.{element.Name}";
                    if (element.Name is "Name" or "NameEnglish" && element.Value.IsString)
                    {
                        found.Add($"{child}={element.Value.AsString}");
                    }
                    else
                    {
                        NamesOf(element.Value, child, found);
                    }
                }

                break;
            case BsonArray array:
                foreach (var item in array)
                {
                    NamesOf(item, path, found);
                }

                break;
        }
    }

    /// <summary>The same documents in the same collections, and if not, which ones are on one side only.</summary>
    static void AssertSame(
        SortedDictionary<string, string[]> expected,
        SortedDictionary<string, string[]> actual,
        string when = ""
    )
    {
        var differences = new List<string>();
        foreach (var collection in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
        {
            var one = expected.GetValueOrDefault(collection, []);
            var another = actual.GetValueOrDefault(collection, []);
            differences.AddRange(one.Except(another).Select(x => $"{collection} only in the expected: {x}"));
            differences.AddRange(another.Except(one).Select(x => $"{collection} only in the actual: {x}"));
        }

        Assert.True(
            differences.Count == 0,
            $"{when}{Environment.NewLine}{string.Join(Environment.NewLine, differences)}"
        );
    }

    static async Task<List<BsonDocument>> ReadAsync(IMongoDatabase database, string collection)
    {
        return await database
            .GetCollection<BsonDocument>(collection)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync();
    }

    static async Task<BsonDocument> FindAsync(IMongoDatabase database, string collection, Guid id)
    {
        var found = await database
            .GetCollection<BsonDocument>(collection)
            .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
            .FirstOrDefaultAsync();
        return found ?? throw new Xunit.Sdk.XunitException($"{collection} has no document {id}.");
    }

    async Task<IMongoDatabase> SeededAsync()
    {
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase("apply-" + Guid.NewGuid().ToString("N"));
        await LegacyWorldData.SeedAsync(database);
        return database;
    }

    static IMongoCollection<BsonDocument> Collection(IMongoDatabase database, string name)
    {
        return database.GetCollection<BsonDocument>(name);
    }

    static Task<ApplyResult> ApplyAsync(
        IMongoDatabase database,
        Action<string>? afterStep = null,
        IdentityDecisions? decisions = null,
        int pageSize = ThrottledReads.PAGE_SIZE
    )
    {
        return IdentitiesMigration.Apply(
            database,
            decisions ?? IdentityDecisions.None,
            new ApplyOptions
            {
                Now = LegacyWorldData.NOW,
                PageSize = pageSize,
                AfterStep = step =>
                {
                    afterStep?.Invoke(step);
                    return Task.CompletedTask;
                },
            }
        );
    }

    sealed class StoppedException : Exception;
}
