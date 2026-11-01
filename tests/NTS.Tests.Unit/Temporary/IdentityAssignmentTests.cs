using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.IdentityMigration;
using static NTS.Tests.Unit.Temporary.IdentityMigrationDocuments;

namespace NTS.Tests.Unit.Temporary;

/// <summary>
/// #637, ADR-0010: which Guid each Athlete and each Horse ends with, and what becomes of the Setup rows: the survivor of a cluster of
/// rows, the row that is made for an identity that has none, the rows that merge away, and what stops the run because the data and the
/// decisions disagree with a Guid that is written.
/// </summary>
public sealed class IdentityAssignmentTests
{
    static readonly BsonDocument BULGARIA = Country("Bulgaria", "BG", "BUL");
    static readonly Dictionary<string, DateTime> DATES = new()
    {
        ["900"] = new DateTime(2026, 4, 25, 21, 0, 0, DateTimeKind.Utc),
        ["901"] = new DateTime(2025, 8, 31, 21, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void An_identity_with_a_Setup_row_takes_the_Guid_of_the_row_and_so_does_each_of_its_copies()
    {
        var plan = Plan(
            ("athletes", Person(7, "Stefan Kolev", fei: "100")),
            Ride(70, 900, Person(7, "Stefan Kolev", fei: "100")),
            Ride(80, 901, Person(0, "STEFAN KOLEV"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
        var identity = OneAthlete(assignments);
        Assert.Equal(LegacyGuids.Of("Athlete", null, 7), identity.Id);
        Assert.Equal("7", identity.Survivor);
        Assert.Empty(identity.Merged);
        Assert.Null(identity.Create);
        foreach (var spelling in plan.Athletes.Spellings)
        {
            Assert.Equal(identity.Id, assignments.GuidOf(IdentityKind.Athlete, spelling.Key));
        }
    }

    [Fact]
    public void Of_several_rows_the_one_the_most_Participations_ride_on_survives_and_the_others_merge_away()
    {
        var plan = Plan(
            ("athletes", Person(4, "Maria Georgieva")),
            ("athletes", Person(5, "Maria Georgieva")),
            Ride(70, 900, Person(5, "Maria Georgieva")),
            Ride(71, 900, Person(5, "Maria Georgieva")),
            Ride(80, 901, Person(4, "Maria Georgieva"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("5", identity.Survivor);
        Assert.Equal(["4"], identity.Merged);
        Assert.Equal(LegacyGuids.Of("Athlete", null, 5), identity.Id);
    }

    [Fact]
    public void Of_rows_that_tie_the_lowest_id_survives()
    {
        var plan = Plan(
            ("athletes", Person(12, "Maria Georgieva")),
            ("athletes", Person(5, "Maria Georgieva")),
            ("athletes", Person(30, "Maria Georgieva"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("5", identity.Survivor);
        Assert.Equal(["12", "30"], identity.Merged.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_lowest_id_is_the_lowest_number_and_not_the_first_text()
    {
        var plan = Plan(("athletes", Person(100, "Maria Georgieva")), ("athletes", Person(9, "Maria Georgieva")));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("9", identity.Survivor);
    }

    [Fact]
    public void An_identity_with_no_row_takes_the_Guid_of_its_lowest_id_that_is_not_zero_and_has_a_row_made()
    {
        var plan = Plan(
            Ride(70, 900, Person(12, "Petar Dimitrov")),
            Ride(80, 901, Person(9, "Petar Dimitrov")),
            Ride(81, 901, Person(0, "Petar Dimitrov"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal(LegacyGuids.Of("Athlete", null, 9), identity.Id);
        Assert.Null(identity.Survivor);
        Assert.NotNull(identity.Create);
    }

    [Fact]
    public void An_identity_with_no_id_takes_the_Guid_of_its_smallest_FEI_ID_and_without_one_of_its_smallest_name()
    {
        var withFei = Plan(
            Ride(70, 900, Person(0, "Yordan Ivanov", fei: "00300")),
            Ride(80, 901, Person(0, "Iordan Ivanov", fei: "300"))
        );
        var withoutFei = Plan(Ride(70, 900, Person(0, "Todor Ivanov")), Ride(80, 901, Person(0, "TODOR  IVANOV")));

        var one = OneAthlete(IdentityAssignments.Build(withFei, DATES, []));
        var another = OneAthlete(IdentityAssignments.Build(withoutFei, DATES, []));

        Assert.Equal(LegacyGuids.Of("Athlete", null, "fei:300"), one.Id);
        Assert.Equal(LegacyGuids.Of("Athlete", null, "name:todor ivanov"), another.Id);
    }

    [Fact]
    public void A_Horse_has_the_Guids_of_the_Horse_space()
    {
        var plan = Plan(("horses", Person(20, "Burya")), Ride(70, 900, Person(1, "A"), Person(20, "Burya")));

        var identity = Assert.Single(
            IdentityAssignments.Build(plan, DATES, []).Identities,
            x => x.Kind == IdentityKind.Horse
        );

        Assert.Equal(LegacyGuids.Of("Horse", null, 20), identity.Id);
    }

    [Fact]
    public void A_cluster_that_has_a_Guid_already_keeps_it_and_the_row_of_that_Guid_survives()
    {
        var written = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var plan = Plan(
            ("athletes", Person(new BsonBinaryData(written, GuidRepresentation.Standard), "Maria Georgieva")),
            ("athletes", Person(5, "Maria Georgieva")),
            Ride(70, 900, Person(new BsonBinaryData(written, GuidRepresentation.Standard), "Maria Georgieva"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal(written, identity.Id);
        Assert.Equal(written.ToString(), identity.Survivor);
        Assert.Equal(["5"], identity.Merged);
    }

    [Fact]
    public void A_cluster_whose_Guid_is_in_its_copies_only_gives_that_Guid_to_the_row_that_is_left()
    {
        var written = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var plan = Plan(
            ("athletes", Person(5, "Maria Georgieva")),
            Ride(70, 900, Person(new BsonBinaryData(written, GuidRepresentation.Standard), "Maria Georgieva"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal(written, identity.Id);
        Assert.Equal("5", identity.Survivor);
    }

    [Fact]
    public void Two_rows_of_one_name_that_have_Guids_of_their_own_stay_as_they_are_when_no_decision_joins_them()
    {
        // duplicates in a registry that is converted already are the registry's, and no run of this refuses on them
        var plan = Plan(
            ("athletes", Person(new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), "Maria Georgieva")),
            ("athletes", Person(new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), "Maria Georgieva"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
    }

    [Fact]
    public void A_decision_that_joins_two_identities_that_have_Guids_of_their_own_stops_the_run_because_it_contradicts_what_is_written()
    {
        const string KEY = "athlete: nikolai ivanov <> athlete: nikolay ivanov";
        var plan = PlanWith(
            $$"""{ "items": { "{{KEY}}": "same" } }""",
            ("athletes", Person(new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), "Nikolay Ivanov")),
            ("athletes", Person(new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), "Nikolai Ivanov"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var refusal = Assert.Single(assignments.Refusals);
        Assert.Contains("two Guids", refusal);
        Assert.Contains("Nikola", refusal);
    }

    [Fact]
    public void Copies_that_carry_one_Guid_are_one_identity_even_when_what_joined_them_has_gone()
    {
        // an earlier run joined the second copy to the row by its legacy id, which is the Guid now, and the names are a near-miss apart
        var written = new BsonBinaryData(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            GuidRepresentation.Standard
        );
        var plan = Plan(
            ("athletes", Person(written, "Maria Georgieva")),
            Ride(70, 900, Person(written, "Maria Georgiev"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
        var identity = OneAthlete(assignments);
        Assert.Equal(Guid.Parse("33333333-3333-4333-8333-333333333333"), identity.Id);
        Assert.Equal("33333333-3333-4333-8333-333333333333", identity.Survivor);
        Assert.Empty(identity.Merged);
        Assert.Null(identity.Create);
        Assert.Equal(2, identity.Spellings.Count);
    }

    [Fact]
    public void A_decision_of_different_for_copies_that_carry_one_Guid_stops_the_run_because_it_contradicts_what_is_written()
    {
        var written = new BsonBinaryData(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            GuidRepresentation.Standard
        );
        var copies = new[]
        {
            Ride(70, 900, Person(written, "Stefan Kolev", fei: "100")),
            Ride(71, 900, Person(written, "Stefan Kolev", fei: "101")),
        };
        var key = "athlete (fei 100): stefan kolev <> athlete (fei 101): stefan kolev";

        var undecided = IdentityAssignments.Build(Plan(copies), DATES, []);
        var decided = IdentityAssignments.Build(
            IdentityPlan.Build(
                [.. copies.SelectMany(x => IdentityCopies.Read(x.Item1, x.Item2))],
                new Dictionary<string, string>(),
                IdentityDecisions.Parse($$"""{ "items": { "{{key}}": "different" } }""")
            ),
            DATES,
            []
        );

        Assert.Empty(undecided.Refusals);
        Assert.Single(undecided.Identities, x => x.Kind == IdentityKind.Athlete);
        var refusal = Assert.Single(decided.Refusals);
        Assert.Contains(key, refusal);
        Assert.Contains("33333333-3333-4333-8333-333333333333", refusal);
    }

    [Fact]
    public void A_rule_of_a_class_that_says_different_does_not_stop_the_run_for_copies_that_carry_one_Guid()
    {
        var written = new BsonBinaryData(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            GuidRepresentation.Standard
        );
        var copies = new[]
        {
            ("athletes", Person(written, "Maria Georgieva")),
            Ride(70, 900, Person(written, "Maria Georgiev")),
        };
        var plan = IdentityPlan.Build(
            [.. copies.SelectMany(x => IdentityCopies.Read(x.Item1, x.Item2))],
            new Dictionary<string, string>(),
            IdentityDecisions.Parse("""{ "rules": { "NearMiss": "different" } }""")
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
        Assert.Single(assignments.Identities, x => x.Kind == IdentityKind.Athlete);
    }

    [Fact]
    public void Two_identities_that_share_a_legacy_id_do_not_share_a_Guid()
    {
        var plan = Plan(
            Ride(70, 900, Person(9, "Stefan Kolev", fei: "100")),
            Ride(71, 900, Person(9, "Stefan Kolev", fei: "101"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var athletes = assignments.Identities.Where(x => x.Kind == IdentityKind.Athlete).ToList();
        Assert.Equal(2, athletes.Count);
        Assert.Equal(2, athletes.Select(x => x.Id).Distinct().Count());
        Assert.Contains(athletes, x => x.Id == LegacyGuids.Of("Athlete", null, 9));
    }

    [Fact]
    public void A_row_that_is_made_is_built_from_the_latest_copy_in_Cyrillic_with_the_Latin_spelling_beside_it()
    {
        var plan = Plan(
            Ride(80, 901, Person(0, "Ivan Petrov", country: Country("Bulgaria", "BGR"))),
            Ride(70, 900, Person(0, "Иван Петров", "Ivan Petrov", fei: "100", country: BULGARIA, club: "Sofia")),
            Ride(71, 900, Person(0, "IVAN PETROV"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        var row = identity.Create!;
        Assert.Equal("Иван Петров", row["Name"].AsString);
        Assert.Equal("Ivan Petrov", row["NameEnglish"].AsString);
        Assert.Equal("100", row["FeiId"].AsString);
        Assert.Equal("Sofia", row["Club"]["Name"].AsString);
        Assert.Equal("nts", row["TenantId"].AsString);
    }

    [Fact]
    public void A_row_that_is_made_is_in_Latin_when_no_copy_is_in_Cyrillic_and_has_no_English_name_that_is_the_same()
    {
        var plan = Plan(Ride(70, 900, Person(0, "Petar Dimitrov", "Petar Dimitrov")));

        var row = OneAthlete(IdentityAssignments.Build(plan, DATES, [])).Create!;

        Assert.Equal("Petar Dimitrov", row["Name"].AsString);
        Assert.False(row.Contains("NameEnglish"));
    }

    [Fact]
    public void The_country_of_a_row_that_is_made_is_the_ISO_country_of_the_countries_and_not_the_code_of_the_federation()
    {
        var countries = new List<BsonDocument>
        {
            new()
            {
                ["_id"] = 77,
                ["Name"] = "Bulgaria",
                ["IsoCode"] = "BG",
                ["NfCode"] = "BUL",
            },
            new()
            {
                ["_id"] = 78,
                ["Name"] = "Romania",
                ["IsoCode"] = "RO",
                ["NfCode"] = "ROU",
            },
        };
        var federation = new BsonDocument
        {
            ["_id"] = 0,
            ["Name"] = "Bulgaria",
            ["NfCode"] = "BUL",
        };
        var plan = Plan(Ride(70, 900, Person(0, "Petar Dimitrov", country: federation)));

        var row = OneAthlete(IdentityAssignments.Build(plan, DATES, countries)).Create!;

        Assert.Equal(77, row["Country"]["_id"].AsInt32);
        Assert.Equal("BG", row["Country"]["IsoCode"].AsString);
    }

    [Fact]
    public void An_empty_English_name_of_the_row_that_survives_is_filled_with_the_Latin_spelling_and_one_that_is_there_is_not()
    {
        var empty = Plan(
            ("athletes", Person(1, "Иван Петров")),
            Ride(70, 900, Person(0, "Ivan Petrov")),
            Ride(80, 901, Person(0, "Иван Петров", "Ivan Petrov"))
        );
        var filled = Plan(
            ("athletes", Person(1, "Иван Петров", "I. Petrov")),
            Ride(70, 900, Person(0, "Иван Петров", "Ivan Petrov"))
        );

        var toFill = OneAthlete(IdentityAssignments.Build(empty, DATES, []));
        var notToFill = OneAthlete(IdentityAssignments.Build(filled, DATES, []));

        Assert.Equal("Ivan Petrov", toFill.NameEnglish);
        Assert.Null(notToFill.NameEnglish);
    }

    [Fact]
    public void A_row_in_Latin_letters_is_not_given_an_English_name_because_it_is_one()
    {
        var plan = Plan(
            ("athletes", Person(1, "Iordan Ivanov", fei: "300")),
            Ride(70, 900, Person(0, "Yordan Ivanov", fei: "300"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Null(identity.NameEnglish);
    }

    [Fact]
    public void Nothing_is_filled_when_no_copy_has_another_spelling()
    {
        var plan = Plan(("athletes", Person(1, "Иван Петров")), Ride(70, 900, Person(0, "Иван Петров")));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Null(identity.NameEnglish);
    }

    [Fact]
    public void A_copy_with_no_name_stops_the_run_and_so_does_a_decision_that_matches_nothing()
    {
        var nameless = Plan(Ride(70, 900, Person(0, null)));
        var unused = IdentityPlan.Build(
            [.. IdentityCopies.Read("athletes", Person(1, "Ivan Petrov"))],
            new Dictionary<string, string>(),
            IdentityDecisions.Parse("""{ "items": { "athlete: nobody <> athlete: noone": "same" } }""")
        );

        var one = IdentityAssignments.Build(nameless, DATES, []);
        var another = IdentityAssignments.Build(unused, DATES, []);

        Assert.Contains(one.Refusals, x => x.Contains("no name"));
        Assert.Contains(another.Refusals, x => x.Contains("athlete: nobody <> athlete: noone"));
    }

    [Fact]
    public void A_Setup_row_that_was_corrected_since_is_still_the_row_it_was_and_a_copy_that_says_other_is_not()
    {
        var made = IdentityAssignments.Build(
            Plan(("athletes", Person(1, "Иван Петров")), Ride(70, 900, Person(0, "Иван Петров"))),
            DATES,
            []
        );
        var corrected = Plan(
            ("athletes", Person(1, "Иван Петров", "I. Petrov")),
            Ride(70, 900, Person(0, "Иван Петров"))
        );
        var other = Plan(Ride(70, 900, Person(0, "Иван Петров", "I. Petrov")));

        var row = corrected.Athletes.Spellings.Single(x => x.IsSetupRow);
        var copy = other.Athletes.Spellings.Single();

        Assert.Equal(OneAthlete(made).Id, made.GuidOf(IdentityKind.Athlete, row.Key));
        Assert.Null(made.GuidOf(IdentityKind.Athlete, copy.Key)); // a copy is told by what it says
        Assert.Null(made.GuidOf(IdentityKind.Horse, row.Key)); // and by its kind
    }

    [Fact]
    public void The_assignments_are_the_same_whatever_the_order_the_documents_came_in()
    {
        var documents = new (string, BsonDocument)[]
        {
            ("athletes", Person(4, "Maria Georgieva")),
            ("athletes", Person(5, "Maria Georgieva")),
            Ride(70, 900, Person(5, "Maria Georgieva")),
            Ride(80, 901, Person(11, "Petar Dimitrov")),
            Ride(81, 901, Person(0, "Todor Ivanov")),
        };

        var forward = IdentityAssignments.Build(Plan(documents), DATES, []);
        var backward = IdentityAssignments.Build(Plan([.. documents.Reverse()]), DATES, []);

        Assert.Equal(
            forward.Identities.Select(x => (x.Id, x.Survivor)).Order(),
            backward.Identities.Select(x => (x.Id, x.Survivor)).Order()
        );
    }

    [Fact]
    public void The_assignments_are_kept_as_a_document_and_come_back_the_same()
    {
        var plan = Plan(
            ("athletes", Person(4, "Maria Georgieva")),
            ("athletes", Person(5, "Maria Georgieva")),
            ("athletes", Person(6, "Иван Петров")),
            Ride(70, 900, Person(5, "Maria Georgieva")),
            Ride(71, 900, Person(6, "Ivan Petrov")),
            Ride(80, 901, Person(11, "Petar Dimitrov", country: BULGARIA, club: "Sofia")),
            Ride(81, 901, Person(0, "Todor Ivanov"))
        );
        var made = IdentityAssignments.Build(plan, DATES, []);
        Assert.Contains(made.Identities, x => x.NameEnglish == "Ivan Petrov");

        var kept = IdentityAssignments.FromDocument(made.ToDocument("decisions-hash"));

        Assert.Equal("decisions-hash", kept.Decisions);
        Assert.Equal(made.Identities.Count, kept.Identities.Count);
        foreach (var spelling in plan.Athletes.Spellings)
        {
            Assert.Equal(
                made.GuidOf(IdentityKind.Athlete, spelling.Key),
                kept.GuidOf(IdentityKind.Athlete, spelling.Key)
            );
        }

        Assert.Equal(
            made.Identities.Select(x =>
                (
                    x.Kind,
                    x.Id,
                    x.Survivor,
                    string.Join(',', x.Merged),
                    x.Create?.ToJson(),
                    x.NameEnglish,
                    string.Join('|', x.Spellings)
                )
            ),
            kept.Identities.Select(x =>
                (
                    x.Kind,
                    x.Id,
                    x.Survivor,
                    string.Join(',', x.Merged),
                    x.Create?.ToJson(),
                    x.NameEnglish,
                    string.Join('|', x.Spellings)
                )
            )
        );
    }

    [Fact]
    public void A_decision_that_other_decisions_overrule_stops_the_run()
    {
        const string AB = "athlete: nikolai ivanov <> athlete: nikolay ivanov";
        const string AC = "athlete: nikolaj ivanov <> athlete: nikolay ivanov";
        const string BC = "athlete: nikolai ivanov <> athlete: nikolaj ivanov";
        var plan = PlanWith(
            $$"""{ "items": { "{{AB}}": "same", "{{BC}}": "same", "{{AC}}": "different" } }""",
            ("athletes", Person(1, "Nikolay Ivanov")),
            ("athletes", Person(2, "Nikolai Ivanov")),
            ("athletes", Person(3, "Nikolaj Ivanov"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var refusal = Assert.Single(assignments.Refusals);
        Assert.Contains(AC, refusal);
        Assert.Contains("overruled", refusal);
    }

    [Fact]
    public void A_decision_of_same_that_waits_for_a_group_that_is_held_stops_the_run()
    {
        const string NEXT_TO = "athlete (fei 100, 200): stefan kolev <> athlete: stephan kolev";
        var plan = PlanWith(
            $$"""{ "items": { "{{NEXT_TO}}": "same" } }""",
            ("athletes", Person(1, "Stefan Kolev", fei: "100")),
            ("athletes", Person(2, "Stefan Kolev", fei: "200")),
            ("athletes", Person(3, "Stefan Kolev")),
            ("athletes", Person(4, "Stephan Kolev"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var refusal = Assert.Single(assignments.Refusals);
        Assert.Contains(NEXT_TO, refusal);
        Assert.Contains("waits", refusal);
    }

    [Fact]
    public void An_identity_with_a_row_takes_the_Guid_of_its_row_before_one_with_none_that_would_take_it_too()
    {
        // the same id 9 under two names that share nothing: two people, and the row has the better claim whichever is first in order
        var plan = Plan(("athletes", Person(9, "Bob Young")), Ride(70, 900, Person(9, "Aaron Zed")));

        var athletes = IdentityAssignments
            .Build(plan, DATES, [])
            .Identities.Where(x => x.Kind == IdentityKind.Athlete)
            .ToList();

        Assert.Equal(2, athletes.Count);
        Assert.Equal(LegacyGuids.Of("Athlete", null, 9), Assert.Single(athletes, x => x.Survivor == "9").Id);
        Assert.Equal(
            LegacyGuids.Of("Athlete", null, "name:aaron zed"),
            Assert.Single(athletes, x => x.Survivor == null).Id
        );
    }

    [Fact]
    public void The_Guid_of_a_surviving_row_is_made_of_its_own_id_and_not_of_the_lowest_id_among_the_copies()
    {
        var plan = Plan(("athletes", Person(7, "Stefan Kolev")), Ride(70, 900, Person(3, "Stefan Kolev")));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("7", identity.Survivor);
        Assert.Equal(LegacyGuids.Of("Athlete", null, 7), identity.Id);
    }

    [Fact]
    public void Rows_that_have_no_integer_id_and_tie_are_told_by_their_documents_and_the_lowest_survives()
    {
        var plan = Plan(
            ("athletes", Person(ObjectId.Parse("650000000000000000000002"), "Maria Georgieva")),
            ("athletes", Person(ObjectId.Parse("650000000000000000000001"), "Maria Georgieva"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("650000000000000000000001", identity.Survivor);
        Assert.Equal(["650000000000000000000002"], identity.Merged);
        Assert.Equal(LegacyGuids.Of("Athlete", null, "name:maria georgieva"), identity.Id);
    }

    [Theory]
    [InlineData("""{ "_id": 11, "Name": "Alpha", "IsoCode": "AL", "NfCode": "ALP" }""", 11)] // the id first
    [InlineData("""{ "_id": 0, "Name": "Gamma", "IsoCode": "al", "NfCode": "BET" }""", 10)] // then the ISO code, in any case
    [InlineData("""{ "_id": 0, "Name": "Gamma", "NfCode": "bet" }""", 11)] // then the code of the federation
    [InlineData("""{ "_id": 0, "Name": "gamma" }""", 12)] // then the name
    [InlineData("""{ "_id": 0, "Name": "Nowhere", "IsoCode": "NW" }""", 0)] // and a country that is no row stays as it is
    public void The_country_of_a_row_that_is_made_is_found_in_the_countries_by_the_id_the_ISO_code_the_federation_and_the_name_in_that_order(
        string country,
        int expected
    )
    {
        var countries = new List<BsonDocument>
        {
            BsonDocument.Parse("""{ "_id": 10, "Name": "Alpha", "IsoCode": "AL", "NfCode": "ALP" }"""),
            BsonDocument.Parse("""{ "_id": 11, "Name": "Beta", "IsoCode": "BE", "NfCode": "BET" }"""),
            BsonDocument.Parse("""{ "_id": 12, "Name": "Gamma", "IsoCode": "GA", "NfCode": "GAM" }"""),
        };
        var plan = Plan(Ride(70, 900, Person(0, "Petar Dimitrov", country: BsonDocument.Parse(country))));

        var row = OneAthlete(IdentityAssignments.Build(plan, DATES, countries)).Create!;

        Assert.Equal(expected, row["Country"]["_id"].AsInt32);
        if (expected == 0)
        {
            Assert.Equal("Nowhere", row["Country"]["Name"].AsString);
        }
    }

    [Fact]
    public void The_latest_Event_decides_the_club_and_the_country_of_a_row_that_is_made()
    {
        var plan = Plan(
            Ride(80, 901, Person(0, "Petar Dimitrov", country: Country("Old Country"), club: "Old Club")),
            Ride(70, 900, Person(0, "Petar Dimitrov", country: Country("New Country"), club: "New Club"))
        );

        var row = OneAthlete(IdentityAssignments.Build(plan, DATES, [])).Create!;

        Assert.Equal("New Club", row["Club"]["Name"].AsString);
        Assert.Equal("New Country", row["Country"]["Name"].AsString);
    }

    [Fact]
    public void A_row_that_is_made_is_in_the_Tenant_of_its_document_and_in_the_constant_when_the_document_has_none()
    {
        var bulgarian = Participation(70, 900, Combination(1, Person(0, "Petar Dimitrov"), Person(0, "Burya")));
        bulgarian["TenantId"] = "country-ro";
        var bare = Participation(71, 900, Combination(2, Person(0, "Todor Ivanov"), Person(0, "Nightfall")));
        bare.Remove("TenantId");

        var plan = Plan(("event_participations", bulgarian), ("event_participations", bare));

        var athletes = IdentityAssignments
            .Build(plan, DATES, [])
            .Identities.Where(x => x.Kind == IdentityKind.Athlete)
            .ToList();
        Assert.Equal(
            "country-ro",
            athletes.Single(x => x.Create!["Name"] == "Petar Dimitrov").Create!["TenantId"].AsString
        );
        Assert.Equal("nts", athletes.Single(x => x.Create!["Name"] == "Todor Ivanov").Create!["TenantId"].AsString);
    }

    [Fact]
    public void The_English_name_that_fills_a_row_is_the_latest_copys_and_not_the_one_of_a_row_that_merges_away()
    {
        var plan = Plan(
            ("athletes", Person(1, "Иван Петров")),
            ("athletes", Person(2, "Иван Петров", "I. Petrov")), // merges away; it is older than any Event
            Ride(70, 900, Person(0, "Иван Петров", "Ivan Petrov"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("1", identity.Survivor);
        Assert.Equal("Ivan Petrov", identity.NameEnglish);
    }

    [Fact]
    public void A_row_that_is_made_has_no_English_name_when_its_Name_is_in_Latin_whatever_other_spellings_there_are()
    {
        var plan = Plan(
            Ride(70, 900, Person(0, "Iordan Ivanov", fei: "300")),
            Ride(71, 900, Person(0, "Yordan Ivanov", fei: "300"))
        );

        var row = OneAthlete(IdentityAssignments.Build(plan, DATES, [])).Create!;

        Assert.Equal("Yordan Ivanov", row["Name"].AsString); // the later place of the same Event
        Assert.False(row.Contains("NameEnglish"));
    }

    [Fact]
    public void A_Guid_that_is_written_for_one_identity_is_never_given_to_another()
    {
        // an earlier run gave Aaron Zed the Guid that the id 7 makes, and Bob Young is the row of that id now
        var written = new BsonBinaryData(LegacyGuids.Of("Athlete", null, 7), GuidRepresentation.Standard);
        var plan = Plan(Ride(70, 900, Person(written, "Aaron Zed")), ("athletes", Person(7, "Bob Young")));

        var athletes = IdentityAssignments
            .Build(plan, DATES, [])
            .Identities.Where(x => x.Kind == IdentityKind.Athlete)
            .ToList();

        Assert.Equal(LegacyGuids.Of("Athlete", null, 7), Assert.Single(athletes, x => x.Survivor == null).Id);
        var bob = Assert.Single(athletes, x => x.Survivor == "7");
        Assert.Equal(LegacyGuids.Of("Athlete", null, "name:bob young"), bob.Id);
    }

    [Fact]
    public void An_English_name_that_is_the_name_of_the_row_is_no_English_name_for_it()
    {
        var plan = Plan(("athletes", Person(1, "Иван Петров")), Ride(70, 900, Person(0, "Иван Петров", "Иван Петров")));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Null(identity.NameEnglish);
    }

    [Fact]
    public void Another_spelling_in_Cyrillic_is_no_Latin_spelling()
    {
        var plan = Plan(("athletes", Person(1, "Иван Петров")), Ride(70, 900, Person(0, "Иван  Петров")));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Null(identity.NameEnglish);
    }

    [Theory]
    [InlineData("ivan petrov")]
    [InlineData("IVAN PETROV")]
    public void A_name_in_Latin_letters_of_any_case_is_the_Latin_spelling_that_fills_a_Cyrillic_row(string latin)
    {
        var plan = Plan(("athletes", Person(1, "Иван Петров")), Ride(70, 900, Person(0, latin)));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal(latin, identity.NameEnglish);
    }

    [Fact]
    public void The_row_the_most_Participations_ride_on_counts_each_Participation_once_however_many_copies_there_are()
    {
        var ride70 = Participation(70, 900, Combination(1, Person(3, "Maria Georgieva"), Person(0, "Burya")));
        var plan = Plan(
            ("athletes", Person(3, "Maria Georgieva")),
            ("athletes", Person(9, "Maria Georgieva")),
            ("event_participations", ride70),
            ("event_rankings", Ranking(300, 900, ride70, ride70)), // the same ride, copied into two entries of a Ranking
            Ride(71, 900, Person(9, "Maria Georgieva")),
            Ride(72, 900, Person(9, "Maria Georgieva"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal("9", identity.Survivor); // two rides against one, whatever the copies
    }

    [Fact]
    public void The_smallest_FEI_ID_of_an_identity_that_a_decision_made_one_makes_its_Guid()
    {
        const string KEY = "athlete (fei 100): stefan kolev <> athlete (fei 200): stefan kolev";
        var plan = PlanWith(
            $$"""{ "items": { "{{KEY}}": "same" } }""",
            Ride(70, 900, Person(0, "Stefan Kolev", fei: "200")),
            Ride(71, 900, Person(0, "Stefan Kolev", fei: "100"))
        );

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal(LegacyGuids.Of("Athlete", null, "fei:100"), identity.Id);
    }

    [Fact]
    public void The_smallest_name_of_an_identity_with_no_id_and_no_FEI_ID_makes_its_Guid_whichever_script_it_is_in()
    {
        var plan = Plan(Ride(70, 900, Person(0, "Иван Петров")), Ride(71, 900, Person(0, "Ivan Petrov")));

        var identity = OneAthlete(IdentityAssignments.Build(plan, DATES, []));

        Assert.Equal(LegacyGuids.Of("Athlete", null, "name:ivan petrov"), identity.Id);
    }

    [Fact]
    public void An_identity_whose_every_candidate_is_taken_gets_a_Guid_made_of_its_first_spelling()
    {
        // three people of one name under three FEI IDs; two Guids that were written are the ones the third would take
        var thirdFei = new BsonBinaryData(LegacyGuids.Of("Athlete", null, "fei:101"), GuidRepresentation.Standard);
        var thirdName = new BsonBinaryData(
            LegacyGuids.Of("Athlete", null, "name:stefan kolev"),
            GuidRepresentation.Standard
        );
        var plan = Plan(
            Ride(70, 900, Person(thirdFei, "Stefan Kolev", fei: "100")),
            Ride(71, 900, Person(thirdName, "Stefan Kolev", fei: "102")),
            Ride(72, 900, Person(0, "Stefan Kolev", fei: "101"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
        var third = Assert.Single(
            assignments.Identities,
            x => x.Kind == IdentityKind.Athlete && x.Spellings.Single().Contains("101", StringComparison.Ordinal)
        );
        Assert.Equal(LegacyGuids.Of("Athlete", null, "identity:" + third.Spellings.Single()), third.Id);
    }

    [Fact]
    public void A_decision_of_different_between_an_identity_that_shares_a_Guid_and_another_is_no_contradiction()
    {
        var written = new BsonBinaryData(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            GuidRepresentation.Standard
        );
        const string KEY = "athlete: nikolai ivanov <> athlete: nikolaj ivanov";
        var plan = PlanWith(
            $$"""{ "items": { "{{KEY}}": "different" } }""",
            ("athletes", Person(written, "Nikolay Ivanov")),
            Ride(70, 900, Person(written, "Nikolai Ivanov")),
            Ride(71, 900, Person(0, "Nikolaj Ivanov"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
        Assert.Equal(2, assignments.Identities.Count(x => x.Kind == IdentityKind.Athlete)); // the two that share the Guid, and the third
    }

    [Fact]
    public void Copies_that_read_alike_and_were_given_two_Guids_by_an_earlier_run_are_left_as_they_are()
    {
        // two people of one name that a run kept apart, because a FEI ID held the group; once converted, nothing but the Guid tells their copies apart
        var first = new BsonBinaryData(Guid.Parse("33333333-3333-4333-8333-333333333333"), GuidRepresentation.Standard);
        var second = new BsonBinaryData(
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            GuidRepresentation.Standard
        );
        var hundred = new BsonBinaryData(
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            GuidRepresentation.Standard
        );
        var twoHundred = new BsonBinaryData(
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            GuidRepresentation.Standard
        );
        var plan = Plan(
            Ride(70, 900, Person(first, "Stefan Kolev")),
            Ride(71, 900, Person(second, "Stefan Kolev")),
            Ride(72, 900, Person(hundred, "Stefan Kolev", fei: "100")),
            Ride(73, 900, Person(twoHundred, "Stefan Kolev", fei: "200"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        Assert.Empty(assignments.Refusals);
        var athletes = assignments.Identities.Where(x => x.Kind == IdentityKind.Athlete).ToList();
        Assert.Equal(
            [Guid.Parse("55555555-5555-4555-8555-555555555555"), Guid.Parse("66666666-6666-4666-8666-666666666666")],
            athletes.Select(x => x.Id).Order()
        ); // the two that carry one Guid each; the reading that carries two has nothing to assign
    }

    [Fact]
    public void Copies_of_one_identity_with_two_Guids_and_copies_with_none_are_refused()
    {
        var first = new BsonBinaryData(Guid.Parse("33333333-3333-4333-8333-333333333333"), GuidRepresentation.Standard);
        var second = new BsonBinaryData(
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            GuidRepresentation.Standard
        );
        var plan = Plan(
            Ride(70, 900, Person(first, "Stefan Kolev")),
            Ride(71, 900, Person(second, "Stefan Kolev")),
            Ride(72, 900, Person(0, "Stefan Kolev")),
            Ride(73, 900, Person(0, "Stefan Kolev", fei: "100")),
            Ride(74, 900, Person(0, "Stefan Kolev", fei: "200"))
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var refusal = Assert.Single(assignments.Refusals);
        Assert.Contains("two Guids", refusal);
    }

    [Fact]
    public void Identities_that_share_a_Guid_and_have_two_are_refused_when_any_of_them_still_has_a_copy_to_convert()
    {
        var first = new BsonBinaryData(Guid.Parse("33333333-3333-4333-8333-333333333333"), GuidRepresentation.Standard);
        var second = new BsonBinaryData(
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            GuidRepresentation.Standard
        );
        var twoHundred = new BsonBinaryData(
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            GuidRepresentation.Standard
        );
        var plan = Plan(
            Ride(70, 900, Person(first, "Stefan Kolev")), // the reading with no FEI ID carries two Guids, and shares one with the next
            Ride(71, 900, Person(second, "Stefan Kolev")),
            Ride(72, 900, Person(first, "Stefan Kolev", fei: "100")),
            Ride(73, 900, Person(0, "Stefan Kolev", fei: "100")), // and this one is not converted yet
            Ride(74, 900, Person(twoHundred, "Stefan Kolev", fei: "200")) // which holds the group, so that nothing merges by itself
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var refusal = Assert.Single(assignments.Refusals);
        Assert.Contains("two Guids", refusal);
    }

    [Fact]
    public void A_decision_that_joined_one_of_the_identities_that_share_a_Guid_makes_the_two_Guids_a_contradiction()
    {
        var first = new BsonBinaryData(Guid.Parse("33333333-3333-4333-8333-333333333333"), GuidRepresentation.Standard);
        var second = new BsonBinaryData(
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            GuidRepresentation.Standard
        );
        const string KEY = "athlete: nikolai ivanov <> athlete: nikolay ivanov";
        var plan = PlanWith(
            $$"""{ "items": { "{{KEY}}": "same" } }""",
            ("athletes", Person(first, "Nikolay Ivanov")),
            ("athletes", Person(second, "Nikolai Ivanov")),
            Ride(70, 900, Person(first, "Todor Ivanov")) // a reading of its own, converted, that shares the first Guid
        );

        var assignments = IdentityAssignments.Build(plan, DATES, []);

        var refusal = Assert.Single(assignments.Refusals);
        Assert.Contains("two Guids", refusal);
    }

    [Fact]
    public void Decisions_that_match_nothing_and_a_copy_with_no_name_are_no_trouble_once_everything_is_converted()
    {
        // the data has changed since the decisions were taken, by the run that converted it: that is not a mistake
        var written = new BsonBinaryData(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            GuidRepresentation.Standard
        );
        var horse = new BsonBinaryData(Guid.Parse("55555555-5555-4555-8555-555555555555"), GuidRepresentation.Standard);
        var copies = new[]
        {
            ("athletes", Person(written, "Ivan Petrov")),
            Ride(70, 900, Person(written, null), Person(horse, "Burya")),
        };
        var decisions = IdentityDecisions.Parse("""{ "items": { "athlete: nobody <> athlete: noone": "same" } }""");

        var converted = IdentityAssignments.Build(
            IdentityPlan.Build(
                [.. copies.SelectMany(x => IdentityCopies.Read(x.Item1, x.Item2))],
                new Dictionary<string, string>(),
                decisions
            ),
            DATES,
            []
        );
        var notYet = IdentityAssignments.Build(
            IdentityPlan.Build(
                [
                    .. copies
                        .Append(Ride(71, 900, Person(0, "Ivan Petrov"), Person(horse, "Burya")))
                        .SelectMany(x => IdentityCopies.Read(x.Item1, x.Item2)),
                ],
                new Dictionary<string, string>(),
                decisions
            ),
            DATES,
            []
        );

        Assert.Empty(converted.Refusals);
        Assert.Contains(notYet.Refusals, x => x.Contains("athlete: nobody <> athlete: noone"));
        Assert.Contains(notYet.Refusals, x => x.Contains("no name"));
    }

    static AssignedIdentity OneAthlete(IdentityAssignments assignments)
    {
        return Assert.Single(assignments.Identities, x => x.Kind == IdentityKind.Athlete);
    }

    static IdentityPlan Plan(params (string Collection, BsonDocument Document)[] documents)
    {
        return IdentityPlan.Build(
            [.. documents.SelectMany(x => IdentityCopies.Read(x.Collection, x.Document))],
            DATES.ToDictionary(x => x.Key, x => x.Key),
            IdentityDecisions.None
        );
    }

    static (string, BsonDocument) Ride(int id, int eventId, BsonDocument athlete, BsonDocument? horse = null)
    {
        var ride = Participation(id, eventId, Combination(id, athlete, horse ?? Person(0, "Horse of " + id)));
        ride["TenantId"] = "nts";
        return ("event_participations", ride);
    }

    static IdentityPlan PlanWith(string decisions, params (string Collection, BsonDocument Document)[] documents)
    {
        return IdentityPlan.Build(
            [.. documents.SelectMany(x => IdentityCopies.Read(x.Collection, x.Document))],
            new Dictionary<string, string>(),
            IdentityDecisions.Parse(decisions)
        );
    }
}
