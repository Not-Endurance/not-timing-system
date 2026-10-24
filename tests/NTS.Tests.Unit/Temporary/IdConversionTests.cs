using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.IdentityMigration;
using static NTS.Tests.Unit.Temporary.IdentityMigrationDocuments;

namespace NTS.Tests.Unit.Temporary;

/// <summary>
/// #637, ADR-0009: a document of the data of before becomes the document of the Guids. Every id in the table is converted by its
/// space and its scope, an Athlete or a Horse takes the Guid of its identity, a document that is converted already is left as it is,
/// and what the table cannot convert is told and not guessed.
/// </summary>
public sealed class IdConversionTests
{
    static readonly Guid EVENT_900 = LegacyGuids.Of("Event", null, 900);
    static readonly Guid IVAN = Guid.Parse("11111111-1111-4111-8111-111111111111");
    static readonly Guid BURYA = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public void A_Participation_of_an_Event_takes_the_Guids_of_its_spaces_scoped_to_the_Event_or_to_itself()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "Иван Петров"), Person(20, "Burya")));
        ride["TenantId"] = "nts";
        ride["Phases"] = new BsonArray
        {
            new BsonDocument { ["_id"] = 5, ["Gate"] = "A" },
            new BsonDocument { ["_id"] = 6, ["Gate"] = "B" },
        };

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        Assert.Empty(converted.Problems);
        var document = converted.Document;
        var participation = LegacyGuids.Of("Participation", EVENT_900, 70);
        Assert.Equal(Uuid(participation), document["_id"]);
        Assert.Equal(Uuid(EVENT_900), document["EventId"]);
        Assert.Equal(Uuid(LegacyGuids.Of("Combination", EVENT_900, 1001)), document["Combination"]["_id"]);
        Assert.Equal(Uuid(IVAN), document["Combination"]["Athlete"]["_id"]);
        Assert.Equal(Uuid(BURYA), document["Combination"]["Horse"]["_id"]);
        Assert.Equal(Uuid(LegacyGuids.Of("Phase", participation, 5)), document["Phases"][0]["_id"]);
        Assert.Equal(Uuid(LegacyGuids.Of("Phase", participation, 6)), document["Phases"][1]["_id"]);
        Assert.Contains(
            converted.People,
            x => x.Kind == IdentityKind.Athlete && x.Id == IVAN && x.Path == "Combination.Athlete"
        );
        Assert.Contains(
            converted.People,
            x => x.Kind == IdentityKind.Horse && x.Id == BURYA && x.Path == "Combination.Horse"
        );
        Assert.Equal("nts", document["TenantId"]);
        Assert.Equal(participation, converted.Id);
    }

    [Fact]
    public void Everything_else_in_the_document_is_as_it_was()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "Иван Петров"), Person(20, "Burya")));
        var expected = ride.DeepClone().AsBsonDocument;

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        converted.Document.Remove("_id");
        converted.Document.Remove("EventId");
        converted.Document["Combination"].AsBsonDocument.Remove("_id");
        converted.Document["Combination"]["Athlete"].AsBsonDocument.Remove("_id");
        converted.Document["Combination"]["Horse"].AsBsonDocument.Remove("_id");
        expected.Remove("_id");
        expected.Remove("EventId");
        expected["Combination"].AsBsonDocument.Remove("_id");
        expected["Combination"]["Athlete"].AsBsonDocument.Remove("_id");
        expected["Combination"]["Horse"].AsBsonDocument.Remove("_id");
        Assert.Equal(expected, converted.Document);
    }

    [Fact]
    public void The_document_that_was_given_is_not_changed()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "Иван Петров"), Person(20, "Burya")));
        var before = ride.DeepClone();

        IdConversion.Convert("event_participations", ride, Identities());

        Assert.Equal(before, ride);
    }

    [Fact]
    public void A_document_that_is_converted_already_is_left_as_it_is()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "Иван Петров"), Person(20, "Burya")));
        var once = IdConversion.Convert("event_participations", ride, Identities()).Document;

        var again = IdConversion.Convert("event_participations", once, null);

        Assert.False(again.Changed);
        Assert.Empty(again.Problems);
        Assert.Equal(once, again.Document);
    }

    [Fact]
    public void The_combination_a_Setup_and_its_Event_share_is_one_Guid_and_so_are_the_Event_and_its_Setup()
    {
        var combination = Combination(1, Person(7, "Stefan Kolev"), Person(30, "Thunderbolt"));
        var setup = Setup(900, "Winter ride", [combination], ("CEI 1*", 600, combination));
        var ride = Participation(70, 900, combination);

        var fromSetup = IdConversion.Convert("configure_events", setup, Identities());
        var fromRide = IdConversion.Convert("event_participations", ride, Identities());

        Assert.Equal(EVENT_900, fromSetup.Id);
        Assert.Equal(Uuid(EVENT_900), fromRide.Document["EventId"]);
        Assert.Equal(fromSetup.Document["Combinations"][0]["_id"], fromRide.Document["Combination"]["_id"]);
        Assert.Equal(
            fromSetup.Document["Competitions"][0]["Participations"][0]["Combination"]["_id"],
            fromRide.Document["Combination"]["_id"]
        );
    }

    [Fact]
    public void Things_embedded_in_a_Setup_are_scoped_to_it_and_a_Participation_that_is_started_is_a_space_of_its_own()
    {
        var combination = Combination(1, Person(7, "Stefan Kolev"), Person(30, "Thunderbolt"));
        var setup = Setup(900, "Winter ride", [combination], ("CEI 1*", 600, combination));

        var converted = IdConversion.Convert("configure_events", setup, Identities()).Document;

        Assert.Equal(Uuid(LegacyGuids.Of("Competition", EVENT_900, 500)), converted["Competitions"][0]["_id"]);
        Assert.Equal(
            Uuid(LegacyGuids.Of("SetupParticipation", EVENT_900, 600)),
            converted["Competitions"][0]["Participations"][0]["_id"]
        );
    }

    [Fact]
    public void The_same_integer_in_two_Participations_is_two_Guids_because_it_is_scoped_to_its_document()
    {
        var one = Participation(70, 900, Combination(1, Person(0, "A"), Person(0, "B")));
        one["Phases"] = new BsonArray { new BsonDocument { ["_id"] = 5 } };
        var another = Participation(71, 900, Combination(2, Person(0, "A"), Person(0, "B")));
        another["Phases"] = new BsonArray { new BsonDocument { ["_id"] = 5 } };

        var first = IdConversion.Convert("event_participations", one, Identities());
        var second = IdConversion.Convert("event_participations", another, Identities());

        Assert.NotEqual(first.Document["Phases"][0]["_id"], second.Document["Phases"][0]["_id"]);
    }

    [Fact]
    public void A_Ranking_and_a_Handout_name_the_Participation_by_the_Guid_the_Participation_has()
    {
        var participation = IdConversion.Convert(
            "event_participations",
            Participation(70, 900, Combination(1, Person(0, "A"), Person(0, "B"))),
            Identities()
        );
        var ranking = IdConversion.Convert("event_rankings", ReferencingRanking(300, 900, 70, 71), Identities());
        var handout = IdConversion.Convert(
            "event_handouts",
            new BsonDocument
            {
                ["_id"] = 400,
                ["EventId"] = 900,
                ["ParticipationId"] = 70,
            },
            Identities()
        );

        Assert.Equal(participation.Document["_id"], ranking.Document["Entries"][0]["ParticipationId"]);
        Assert.Equal(participation.Document["_id"], handout.Document["ParticipationId"]);
        Assert.Equal(
            Uuid(LegacyGuids.Of("Participation", EVENT_900, 71)),
            ranking.Document["Entries"][1]["ParticipationId"]
        );
        Assert.Equal(Uuid(LegacyGuids.Of("Ranking", EVENT_900, 300)), ranking.Document["_id"]);
        Assert.Equal(Uuid(LegacyGuids.Of("Handout", EVENT_900, 400)), handout.Document["_id"]);
    }

    [Fact]
    public void A_Setup_row_takes_the_Guid_of_its_identity_and_the_country_and_club_it_carries_the_Guids_of_theirs()
    {
        var row = Person(7, "Stefan Kolev", fei: "100", country: Country("Bulgaria", "BG", "BUL"), club: "Sofia");
        var converted = IdConversion.Convert("athletes", row, Identities());

        Assert.Empty(converted.Problems);
        Assert.Equal(Uuid(IVAN), converted.Document["_id"]);
        Assert.Equal(IVAN, converted.Id);
        Assert.Equal(Uuid(LegacyGuids.Of("Country", null, 1)), converted.Document["Country"]["_id"]);
        Assert.Equal(Uuid(LegacyGuids.Of("Club", null, 7)), converted.Document["Club"]["_id"]);
    }

    [Fact]
    public void A_Core_document_names_the_Event_and_the_account_it_refers_to()
    {
        var official = new BsonDocument
        {
            ["_id"] = 12,
            ["EventId"] = 900,
            ["Name"] = "Ivan",
            ["UserId"] = 3,
        };

        var converted = IdConversion.Convert("event_officials", official, Identities());

        Assert.Equal(Uuid(LegacyGuids.Of("Official", EVENT_900, 12)), converted.Document["_id"]);
        Assert.Equal(Uuid(LegacyGuids.Of("User", null, 3)), converted.Document["UserId"]);
        Assert.Contains(
            converted.References,
            x => x.Target == "event_informations" && x.Id == EVENT_900 && x.Path == "EventId"
        );
        Assert.Contains(
            converted.References,
            x => x.Target == "users" && x.Id == LegacyGuids.Of("User", null, 3) && x.Path == "UserId"
        );
    }

    [Fact]
    public void An_account_that_is_not_linked_has_no_reference_to_resolve()
    {
        var official = new BsonDocument
        {
            ["_id"] = 12,
            ["EventId"] = 900,
            ["Name"] = "Ivan",
        };

        var converted = IdConversion.Convert("event_officials", official, Identities());

        Assert.DoesNotContain(converted.References, x => x.Target == "users");
    }

    [Fact]
    public void What_a_person_keeps_per_Event_has_the_object_id_it_has_and_a_Guid_for_the_rest()
    {
        var objectId = ObjectId.GenerateNewId();
        var session = new BsonDocument
        {
            ["_id"] = objectId,
            ["Id"] = 4,
            ["EventId"] = 900,
            ["UserIdentifier"] = "someone",
        };

        var converted = IdConversion.Convert("event_user_sessions", session, Identities());

        Assert.Equal(objectId, converted.Document["_id"].AsObjectId);
        Assert.Equal(Uuid(LegacyGuids.Of("UserSession", EVENT_900, 4)), converted.Document["Id"]);
        Assert.Equal(Uuid(EVENT_900), converted.Document["EventId"]);
        Assert.Equal(LegacyGuids.Of("UserSession", EVENT_900, 4), converted.Id);
    }

    [Fact]
    public void An_id_that_was_made_a_Guid_is_not_converted_and_an_integer_in_its_place_is_told()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "A"), Person(0, "B")));
        ride["Phases"] = new BsonArray
        {
            new BsonDocument
            {
                ["_id"] = 5,
                ["Events"] = new BsonArray { new BsonDocument { ["_id"] = 9 } },
            },
        };

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        var problem = Assert.Single(converted.Problems);
        Assert.Equal("event_participations", problem.Collection);
        Assert.Equal("70", problem.DocumentId);
        Assert.Equal("Phases[0].Events[0]._id", problem.Path);
    }

    [Fact]
    public void An_integer_where_the_table_has_no_id_is_told()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "A"), Person(0, "B")));
        ride["BrokenId"] = 3;

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        Assert.Contains(converted.Problems, x => x.Path == "BrokenId");
    }

    [Fact]
    public void An_id_that_is_neither_an_integer_nor_a_Guid_is_told_and_not_guessed()
    {
        var ride = Participation(70, "nine hundred", Combination(1, Person(0, "A"), Person(0, "B")));

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        Assert.Contains(converted.Problems, x => x.Path == "EventId");
        Assert.Contains(converted.Problems, x => x.Path == "_id"); // a scope it cannot tell is a Participation it cannot name
    }

    [Fact]
    public void An_Athlete_of_no_identity_is_told()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "Nobody"), Person(20, "Burya")));

        var converted = IdConversion.Convert("event_participations", ride, new FixedIdentities(horse: BURYA));

        Assert.Contains(converted.Problems, x => x.Path == "Combination.Athlete._id");
    }

    [Fact]
    public void An_Athlete_or_a_Horse_that_has_a_Guid_already_is_left_when_no_identities_are_given()
    {
        var ride = Participation(
            70,
            Uuid(EVENT_900),
            Combination(1, Person(Uuid(IVAN), "Иван Петров"), Person(Uuid(BURYA), "Burya"))
        );
        ride["_id"] = Uuid(LegacyGuids.Of("Participation", EVENT_900, 70));
        ride["Combination"]["_id"] = Uuid(LegacyGuids.Of("Combination", EVENT_900, 1001));

        var converted = IdConversion.Convert("event_participations", ride, null);

        Assert.False(converted.Changed);
        Assert.Empty(converted.Problems);
        Assert.Contains(converted.People, x => x.Kind == IdentityKind.Athlete && x.Id == IVAN);
        Assert.Contains(converted.People, x => x.Kind == IdentityKind.Horse && x.Id == BURYA);
    }

    [Fact]
    public void An_id_that_a_double_holds_is_the_same_id_as_the_integer()
    {
        var ride = Participation(70.0, 900.0, Combination(1, Person(0, "A"), Person(0, "B")));

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        Assert.Empty(converted.Problems);
        Assert.Equal(Uuid(LegacyGuids.Of("Participation", EVENT_900, 70)), converted.Document["_id"]);
        Assert.Equal(Uuid(EVENT_900), converted.Document["EventId"]);
    }

    [Fact]
    public void An_id_that_is_a_fraction_is_told_and_not_rounded()
    {
        var ride = Participation(70, 900.5, Combination(1, Person(0, "A"), Person(0, "B")));

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        Assert.Contains(converted.Problems, x => x.Path == "EventId");
    }

    [Fact]
    public void A_reference_that_is_null_stays_null_and_refers_to_nothing()
    {
        var official = new BsonDocument
        {
            ["_id"] = 12,
            ["EventId"] = 900,
            ["UserId"] = BsonNull.Value,
        };

        var converted = IdConversion.Convert("event_officials", official, Identities());

        Assert.Empty(converted.Problems);
        Assert.True(converted.Document["UserId"].IsBsonNull);
        Assert.DoesNotContain(converted.References, x => x.Target == "users");
    }

    [Fact]
    public void An_Athlete_that_has_a_Guid_keeps_it_whatever_the_identities_say_of_a_spelling_like_its_own()
    {
        // a copy that is converted reads like others that had no id, and the identities know the spelling by what those said
        var other = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var ride = Participation(70, 900, Combination(1, Person(Uuid(other), "Иван Петров"), Person(20, "Burya")));

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        Assert.DoesNotContain(converted.Problems, x => x.Path == "Combination.Athlete._id");
        Assert.Equal(Uuid(other), converted.Document["Combination"]["Athlete"]["_id"]);
        Assert.Contains(converted.People, x => x.Kind == IdentityKind.Athlete && x.Id == other);
    }

    [Fact]
    public void What_an_Athlete_stored_as_its_User_is_left_as_it_is_and_is_no_problem()
    {
        var row = Person(7, "Stefan Kolev");
        row["User"] = new BsonDocument { ["_id"] = 3, ["Email"] = "official@example.test" };
        var combination = Combination(1, Person(7, "Stefan Kolev"), Person(30, "Thunderbolt"));
        combination["Athlete"]["User"] = new BsonDocument { ["_id"] = 3 };

        var fromRow = IdConversion.Convert("athletes", row, Identities());
        var fromRide = IdConversion.Convert("event_participations", Participation(70, 900, combination), Identities());

        Assert.Empty(fromRow.Problems);
        Assert.Empty(fromRide.Problems);
        Assert.Equal(3, fromRow.Document["User"]["_id"].AsInt32);
        Assert.Equal(3, fromRide.Document["Combination"]["Athlete"]["User"]["_id"].AsInt32);
    }

    [Fact]
    public void What_an_Athlete_stored_as_its_User_in_a_Setup_is_left_as_it_is_too()
    {
        var combination = Combination(1, Person(7, "Stefan Kolev"), Person(30, "Thunderbolt"));
        combination["Athlete"]["User"] = new BsonDocument { ["_id"] = 3 };
        var setup = Setup(900, "Winter ride", [combination], ("CEI 1*", 600, combination));

        var converted = IdConversion.Convert("configure_events", setup, Identities());

        Assert.Empty(converted.Problems);
        Assert.Equal(3, converted.Document["Combinations"][0]["Athlete"]["User"]["_id"].AsInt32);
        Assert.Equal(
            3,
            converted.Document["Competitions"][0]["Participations"][0]["Combination"]["Athlete"]["User"]["_id"].AsInt32
        );
    }

    [Fact]
    public void A_person_in_a_Ranking_that_still_holds_the_copy_of_a_Participation_is_told_and_not_converted()
    {
        var ranking = ReferencingRanking(300, 900, 70);
        ranking["Entries"][0].AsBsonDocument["Participation"] = Participation(
            70,
            900,
            Combination(1, Person(0, "A"), Person(0, "B"))
        );

        var converted = IdConversion.Convert("event_rankings", ranking, Identities());

        Assert.Contains(converted.Problems, x => x.Path == "Entries[0].Participation.Combination.Athlete._id");
        Assert.Contains(converted.Problems, x => x.Path == "Entries[0].Participation.Combination.Horse._id");
        Assert.Equal(0, converted.Document["Entries"][0]["Participation"]["Combination"]["Athlete"]["_id"].AsInt32);
    }

    [Fact]
    public void A_person_where_the_table_has_no_id_for_one_is_told_even_when_it_has_a_Guid_already()
    {
        var ride = Participation(70, 900, Combination(1, Person(0, "A"), Person(0, "B")));
        ride["Spare"] = new BsonDocument { ["Athlete"] = Person(Uuid(IVAN), "Иван Петров") };

        var converted = IdConversion.Convert("event_participations", ride, Identities());

        var problem = Assert.Single(converted.Problems, x => x.Path == "Spare.Athlete._id");
        Assert.Contains("no id", problem.Message);
    }

    static BsonBinaryData Uuid(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    static FixedIdentities Identities()
    {
        return new FixedIdentities(IVAN, BURYA);
    }

    /// <summary>The identities of a test: every Athlete is one person and every Horse is one animal, if they were given.</summary>
    sealed class FixedIdentities : IIdentityGuids
    {
        readonly Guid? _athlete;
        readonly Guid? _horse;

        public FixedIdentities(Guid? athlete = null, Guid? horse = null)
        {
            _athlete = athlete;
            _horse = horse;
        }

        public Guid? GuidOf(IdentityKind kind, string spellingKey)
        {
            return kind == IdentityKind.Athlete ? _athlete : _horse;
        }
    }
}
