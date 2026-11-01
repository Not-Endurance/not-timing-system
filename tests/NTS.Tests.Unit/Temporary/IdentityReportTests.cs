using MongoDB.Bson;
using NTS.Tools.IdentityMigration;
using static NTS.Tests.Unit.Temporary.IdentityMigrationDocuments;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: the Markdown report of the dry run, which the owner reads before deciding anything.</summary>
public sealed class IdentityReportTests
{
    static readonly Dictionary<string, string> EVENTS = new()
    {
        ["900"] = "2026-04-26 to 2026-04-28 Spring ride",
        ["901"] = "2025-09-01 Autumn ride",
    };

    [Fact]
    public void The_report_opens_with_the_counts_of_the_Athletes_and_the_Horses()
    {
        var report = Report(Spring());

        Assert.StartsWith("# Identity migration: dry run\n", report);
        Assert.Contains("Database: nts", report);
        Assert.Contains("Nothing was written to the database.", report);
        Assert.Contains("| | Athletes | Horses |", report);
        Assert.Contains("| Copies read | 11 | 7 |", report);
        Assert.Contains("| Copies in Setup rows | 5 | 1 |", report);
        Assert.Contains("| Copies in configure events | 0 | 0 |", report);
        Assert.Contains("| Copies in Participations | 4 | 4 |", report);
        Assert.Contains("| Copies in Rankings | 2 | 2 |", report);
        Assert.Contains("| Copies in Handouts | 0 | 0 |", report);
        Assert.Contains("| Distinct spellings | 9 | 5 |", report);
        Assert.Contains("| Distinct legacy ids (0 is none) | 6 | 4 |", report);
        Assert.Contains("| Identities found | 5 | 3 |", report);
        Assert.Contains("| Setup rows that exist | 5 | 1 |", report);
        Assert.Contains("| Rows that would be created | 1 | 2 |", report);
        Assert.Contains("| Setup duplicates that would merge | 1 | 0 |", report);
        Assert.Contains("| Copies whose Setup row is gone | 1 | 2 |", report);
        Assert.Contains("| Left to review | 1 | 0 |", report);
        Assert.Contains("| Decided | 0 | 0 |", report);
        Assert.Contains("- Decided by item: 0, by rule: 0", report);
    }

    [Fact]
    public void Every_automatic_merge_is_listed_under_the_rule_that_made_it_with_the_spellings_involved()
    {
        var report = Report(Spring());

        var feiId = Section(report, "### Same FEI ID (1)");
        Assert.Contains(
            "Athlete: \"Иван Петров / Ivan Petrov\" (FEI ID 100, legacy id 1, Setup row) = \"Иван Петров\" (FEI ID 100, legacy id 1, 3 copies)",
            feiId
        );
        var exact = Section(report, "### Same name (6)");
        Assert.Contains(
            "Athlete: \"Maria Georgieva\" (legacy id 4, Setup row) = \"Maria Georgieva\" (legacy id 5, Setup row)",
            exact
        );
        Assert.Contains("Horse: \"Burya\" (legacy id 20, Setup row) = \"Burya\" (legacy id 20, 3 copies)", exact);
        var script = Section(report, "### Same name in another script (1)");
        Assert.Contains("\"IVAN PETROV\" (1 copy)", script);
        Assert.Contains("\"Иван Петров\" (FEI ID 100, legacy id 1, 3 copies)", script);
        Assert.Contains("### Same legacy id (0)", report);
        var headings = new[]
        {
            "### Same FEI ID (",
            "### Same name (",
            "### Same name in another script (",
            "### Same legacy id (",
        };
        var positions = headings.Select(x => report.IndexOf(x, StringComparison.Ordinal)).ToList();
        Assert.All(positions, x => Assert.True(x > 0));
        Assert.Equal(positions.Order(), positions); // the rules are told in the order of their precedence
    }

    [Fact]
    public void The_Setup_duplicates_and_the_rows_that_would_be_created_are_listed()
    {
        var report = Report(Spring());

        var duplicates = Section(report, "## Setup duplicates that would merge (1)");
        Assert.Contains(
            "- Athlete: \"Maria Georgieva\" (legacy id 4, Setup row) + \"Maria Georgieva\" (legacy id 5, Setup row)\n",
            duplicates
        );
        Assert.DoesNotContain("Иван Петров", duplicates); // one row is no duplicate
        var created = Section(report, "## Rows that would be created (3)");
        Assert.Contains("Athlete: \"Petar Dimitrov\" (legacy id 9, 1 copy)", created);
        Assert.Contains("Horse: \"Vihar\" (legacy id 21, 1 copy)", created);
        Assert.Contains("Horse: \"Zora\" (legacy id 22, 1 copy)", created);
    }

    [Fact]
    public void An_item_to_review_shows_the_key_the_class_and_the_evidence_of_every_spelling()
    {
        var report = Report(Spring());

        var item = Section(report, "## To review (1)");
        Assert.Contains("### Spelling variants (1)", item);
        Assert.Contains("#### `athlete: nikolai ivanov <> athlete: nikolay ivanov`", item);
        Assert.Contains("\"Nikolai Ivanov\" (legacy id 3, Setup row)", item);
        Assert.Contains("\"Nikolai Ivanov\" (legacy id 3, 1 copy)", item);
        Assert.Contains("\"Nikolay Ivanov\" (legacy id 2, Setup row)", item);
        Assert.Contains("  - Copies: Setup 1\n", item);
        Assert.Contains("Copies: Participations 1", item);
        Assert.Contains("Events: 2026-04-26 to 2026-04-28 Spring ride", item);
        Assert.Contains("Entered with: Burya", item);
        Assert.Contains("spelling variant", item);
        Assert.Contains("Never in the same Event.", item);
    }

    [Fact]
    public void The_evidence_has_the_FEI_ID_the_country_as_stored_the_club_the_Events_and_the_partners()
    {
        var spring = Spring();
        spring.Add(("athletes", Person(8, "Yvan Petrov"))); // a spelling variant of Ivan Petrov, so that his group is reviewed
        var report = Report(spring);

        var item = Section(report, "## To review (2)");
        Assert.Contains("#### `athlete (fei 100): ivan petrov | иван петров <> athlete: yvan petrov`", item);
        Assert.Contains("\"Иван Петров / Ivan Petrov\" (FEI ID 100, legacy id 1, Setup row)", item);
        Assert.Contains("Country: Bulgaria (BG/BUL)", item);
        Assert.Contains("Country: Bulgaria (BGR)", item); // the copy in the ride writes it otherwise
        Assert.Contains("Club: Sofia", item);
        Assert.Contains("Copies: Participations 1, Rankings 2 (1 ride)", item); // a Participation in two Rankings is one ride
        Assert.Contains("Events: 2026-04-26 to 2026-04-28 Spring ride", item);
        Assert.Contains("Entered with: Burya", item);
    }

    [Fact]
    public void Two_spellings_that_were_in_the_same_Event_are_said_to_have_been()
    {
        var documents = new List<(string, BsonDocument)>
        {
            (
                "event_participations",
                Participation(70, 900, Combination(1, Person(1, "Nikolay Ivanov"), Person(20, "Burya")))
            ),
            (
                "event_participations",
                Participation(71, 900, Combination(2, Person(2, "Nikolai Ivanov"), Person(21, "Vihar")))
            ),
        };

        var report = Report(documents);

        Assert.Contains("Seen in the same Event: 2026-04-26 to 2026-04-28 Spring ride", report);
    }

    [Fact]
    public void A_held_group_is_listed_whole_with_the_spellings_that_disagree()
    {
        var report = Report(
            [
                ("athletes", Person(1, "Stefan Kolev", fei: "100")),
                ("athletes", Person(2, "Stefan Kolev", fei: "200")),
                ("athletes", Person(3, "Stefan Kolev")),
            ]
        );

        var held = Section(report, "### Held groups (1)");
        Assert.Contains("#### `athlete (fei 100, 200): stefan kolev`", held);
        Assert.Contains("\"Stefan Kolev\" (FEI ID 100, legacy id 1, Setup row)", held);
        Assert.Contains("\"Stefan Kolev\" (FEI ID 200, legacy id 2, Setup row)", held);
        Assert.Contains("Held group. Two of its spellings disagree", held);
        Assert.Contains("They disagree:", held);
        Assert.Contains("conflict", held);
        var merges = Section(report, "### Same name (2)");
        var marked = merges
            .Split('\n')
            .Count(x => x.EndsWith("(in a held group: not merged until it is decided)", StringComparison.Ordinal));
        Assert.Equal(2, marked);
    }

    [Fact]
    public void A_decided_item_is_no_longer_in_the_review_and_a_merge_by_decision_is_listed()
    {
        var report = Report(
            Spring(),
            """{ "items": { "athlete: nikolai ivanov <> athlete: nikolay ivanov": "same" } }"""
        );

        Assert.Contains("| Left to review | 0 | 0 |", report);
        Assert.Contains("Nothing is left to review.", report);
        Assert.DoesNotContain("## To review", report);
        var merges = Section(report, "## Merges by your decisions (1)");
        Assert.Contains("`athlete: nikolai ivanov <> athlete: nikolay ivanov`", merges);
        Assert.Contains("| Identities found | 4 | 3 |", report);
        Assert.Contains("- Decided by item: 1, by rule: 0", report);
        Assert.Contains("| Decided | 1 | 0 |", report);
    }

    [Fact]
    public void A_rule_settles_a_class_and_is_counted_apart()
    {
        var report = Report(Spring(), """{ "rules": { "SpellingVariant": "different" } }""");

        Assert.Contains("| Left to review | 0 | 0 |", report);
        Assert.Contains("- Decided by item: 0, by rule: 1", report);
        Assert.Contains("| Decided | 1 | 0 |", report);
        Assert.Contains("| Identities found | 5 | 3 |", report);
    }

    [Fact]
    public void Decisions_that_match_nothing_or_contradict_or_wait_are_said_so_by_their_key()
    {
        var documents = new List<(string, BsonDocument)>
        {
            ("athletes", Person(1, "Nikolay Ivanov")),
            ("athletes", Person(2, "Nikolai Ivanov")),
            ("athletes", Person(3, "Nikolaj Ivanov")),
        };
        var report = Report(
            documents,
            """
            { "items": {
                "athlete: nikolai ivanov <> athlete: nikolay ivanov": "same",
                "athlete: nikolai ivanov <> athlete: nikolaj ivanov": "same",
                "athlete: nikolaj ivanov <> athlete: nikolay ivanov": "different",
                "athlete: nobody <> athlete: nowhere": "same" } }
            """
        );

        Assert.Contains("- Matching nothing in this data: 1", report);
        Assert.Contains("- Contradicting each other: 1", report);
        Assert.Contains("`athlete: nobody <> athlete: nowhere`", Section(report, "### Decisions that match nothing"));
        Assert.Contains(
            "`athlete: nikolaj ivanov <> athlete: nikolay ivanov`",
            Section(report, "### Decisions that contradict")
        );
    }

    [Fact]
    public void A_copy_with_no_name_is_listed_by_where_it_is()
    {
        var report = Report([("athletes", Person(7, null)), ("athletes", Person(8, "Ivan Petrov"))]);

        var unnamed = Section(report, "## Copies with no name (1)");
        Assert.Contains("Athlete in athletes 7", unnamed);
        Assert.Contains("| Copies with no name (set aside) | 1 | 0 |", report);
    }

    [Fact]
    public void A_Setup_duplicate_is_told_by_its_rows_and_not_by_the_copies_of_the_same_person()
    {
        var report = Report(
            [
                ("athletes", Person(4, "Maria Georgieva")),
                ("athletes", Person(5, "Maria Georgieva")),
                (
                    "event_participations",
                    Participation(73, 900, Combination(4, Person(0, "Maria Georgieva"), Person(23, "Burya")))
                ),
            ]
        );

        var duplicates = Section(report, "## Setup duplicates that would merge (1)");
        Assert.Contains(
            "- Athlete: \"Maria Georgieva\" (legacy id 4, Setup row) + \"Maria Georgieva\" (legacy id 5, Setup row)\n",
            duplicates
        );
    }

    [Fact]
    public void A_report_of_copies_that_all_have_names_has_no_list_of_those_that_have_none()
    {
        var report = Report(Spring());

        Assert.DoesNotContain("## Copies with no name", report);
        Assert.Contains("| Copies with no name (set aside) | 0 | 0 |", report);
    }

    [Fact]
    public void A_person_with_no_Setup_row_is_told_by_all_the_spellings_of_the_copies_in_the_rows_to_create()
    {
        var report = Report(
            [
                (
                    "event_participations",
                    Participation(70, 900, Combination(1, Person(5, "Ivan Petrov"), Person(20, "Burya")))
                ),
                (
                    "event_participations",
                    Participation(71, 901, Combination(2, Person(6, "Ivan Petrov"), Person(20, "Burya")))
                ),
            ]
        );

        var created = Section(report, "## Rows that would be created (2)");
        Assert.Contains(
            "- Athlete: \"Ivan Petrov\" (legacy id 5, 1 copy) = \"Ivan Petrov\" (legacy id 6, 1 copy)\n",
            created
        );
        Assert.Contains("| Rows that would be created | 1 | 1 |", report);
        Assert.Contains("| Copies whose Setup row is gone | 2 | 2 |", report); // the copies of the person, and not the rows
    }

    [Fact]
    public void A_near_miss_of_Horses_is_told_with_the_Setup_row_among_the_copies()
    {
        var report = Report([("horses", Person(30, "Thunderbolt")), ("horses", Person(31, "Thunderbold"))]);

        var item = Section(report, "#### `horse: thunderbold <> horse: thunderbolt`");
        Assert.Contains("  - Copies: Setup 1\n", item);
        Assert.Contains(": near miss\n", item);
    }

    [Fact]
    public void The_report_is_the_same_for_the_same_data_in_any_order_and_carries_no_date()
    {
        var forward = Report(Spring());
        var backward = Report(Spring().AsEnumerable().Reverse());

        Assert.Equal(forward, backward);
        Assert.DoesNotMatch(@"\b20[2-3]\d-\d\d-\d\d \d\d:\d\d", forward); // a date and a time of day: when it was made
        Assert.DoesNotContain("\r", forward);
    }

    [Fact]
    public void A_decision_that_waits_for_a_held_group_says_which_group_to_decide_first()
    {
        var documents = new List<(string, BsonDocument)>
        {
            ("athletes", Person(1, "Stefan Kolev", fei: "100")),
            ("athletes", Person(2, "Stefan Kolev", fei: "200")),
            ("athletes", Person(3, "Stefan Kolev")),
            ("athletes", Person(4, "Stephan Kolev")),
        };

        var report = Report(
            documents,
            """{ "items": { "athlete (fei 100, 200): stefan kolev <> athlete: stephan kolev": "same" } }"""
        );

        Assert.Contains("- Waiting for a held group: 1", report);
        var waiting = Section(report, "### Decisions that wait for a held group (1)");
        Assert.Contains(
            "it applies only when the held group `athlete (fei 100, 200): stefan kolev` is decided `same`",
            waiting
        );
        Assert.DoesNotContain("## Merges by your decisions (1)", report);
    }

    [Fact]
    public void A_merge_made_by_a_rule_says_which_rule()
    {
        var report = Report(Spring(), """{ "rules": { "SpellingVariant": "same" } }""");

        var merges = Section(report, "## Merges by your decisions (1)");
        Assert.Contains(
            "`athlete: nikolai ivanov <> athlete: nikolay ivanov` (the rule for spelling variants: same)",
            merges
        );
    }

    [Fact]
    public void The_Setup_row_is_the_first_line_of_a_group_and_the_copies_come_after_it()
    {
        var report = Report(Spring());

        var item = Section(report, "#### `athlete: nikolai ivanov <> athlete: nikolay ivanov`");
        Assert.True(
            item.IndexOf("(legacy id 3, Setup row)", StringComparison.Ordinal)
                < item.IndexOf("(legacy id 3, 1 copy)", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void The_Athletes_are_told_before_the_Horses_in_every_list()
    {
        var report = Report(Spring());

        var exact = Section(report, "### Same name (");
        Assert.True(
            exact.IndexOf("- Athlete:", StringComparison.Ordinal) < exact.IndexOf("- Horse:", StringComparison.Ordinal)
        );
        var created = Section(report, "## Rows that would be created (");
        Assert.True(
            created.IndexOf("- Athlete:", StringComparison.Ordinal)
                < created.IndexOf("- Horse:", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void An_Event_that_has_no_name_in_the_data_is_told_by_its_id()
    {
        var documents = new List<(string, BsonDocument)>
        {
            (
                "event_participations",
                Participation(70, 999, Combination(1, Person(1, "Nikolay Ivanov"), Person(20, "Burya")))
            ),
            ("athletes", Person(2, "Nikolai Ivanov")),
        };

        var report = Report(documents);

        Assert.Contains("Events: event 999", Section(report, "## To review ("));
    }

    [Fact]
    public void The_evidence_is_in_order_whichever_ride_came_first()
    {
        var documents = new List<(string, BsonDocument)>
        {
            (
                "event_participations",
                Participation(
                    70,
                    900,
                    Combination(
                        1,
                        Person(1, "Nikolay Ivanov", country: Country("Bulgaria", "BGR"), club: "Sofia"),
                        Person(21, "Zora")
                    )
                )
            ),
            (
                "event_participations",
                Participation(
                    71,
                    901,
                    Combination(
                        2,
                        Person(1, "Nikolay Ivanov", country: Country("Bulgaria", "BG", "BUL"), club: "Plovdiv"),
                        Person(20, "Burya")
                    )
                )
            ),
            ("athletes", Person(2, "Nikolai Ivanov")),
        };

        var item = Section(Report(documents), "## To review (");

        Assert.Contains("Events: 2025-09-01 Autumn ride; 2026-04-26 to 2026-04-28 Spring ride", item);
        Assert.Contains("Entered with: Burya, Zora", item);
        Assert.Contains("Country: Bulgaria (BG/BUL), Bulgaria (BGR)", item);
        Assert.Contains("Club: Plovdiv, Sofia", item);
    }

    [Fact]
    public void A_name_in_English_that_is_the_name_in_other_capitals_is_not_told_twice()
    {
        var report = Report(
            [("athletes", Person(1, "IVAN PETROV", "Ivan Petrov")), ("athletes", Person(2, "Yvan Petrov"))]
        );

        Assert.Contains("\"IVAN PETROV\" (legacy id 1, Setup row)", report);
        Assert.DoesNotContain("IVAN PETROV / Ivan Petrov", report);
    }

    [Fact]
    public void A_Setup_row_whose_id_is_not_a_legacy_id_is_told_by_its_id_so_that_two_rows_can_be_told_apart()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();

        var report = Report(
            [
                ("athletes", Person(new BsonBinaryData(id, GuidRepresentation.Standard), "Maria Georgieva")),
                ("athletes", Person(new BsonBinaryData(other, GuidRepresentation.Standard), "Maria Georgieva")),
                ("athletes", Person(5, "Maria Georgieva")),
            ]
        );

        var duplicates = Section(report, "## Setup duplicates that would merge (2)");
        Assert.Contains($"\"Maria Georgieva\" (Setup row {id})", duplicates);
        Assert.Contains($"\"Maria Georgieva\" (Setup row {other})", duplicates);
        Assert.Contains("\"Maria Georgieva\" (legacy id 5, Setup row)", duplicates);
    }

    [Fact]
    public void A_copy_found_only_in_a_configure_event_or_a_Handout_is_told_where_it_is()
    {
        var setup = Setup(902, "Winter ride", [Combination(1, Person(7, "Nikolai Ivanov"), Person(31, "Zora"))]);
        var handout = Handout(
            400,
            900,
            Participation(72, 900, Combination(3, Person(9, "Nikolay Ivanov"), Person(22, "Vihar")))
        );

        var report = Report([("configure_events", setup), ("event_handouts", handout)]);

        var item = Section(report, "## To review (");
        Assert.Contains("Copies: configure events 1", item);
        Assert.Contains("Copies: Handouts 1 (1 ride)", item);
    }

    /// <summary>
    /// A small registry in the shapes the data has: Setup rows, rides of two Events, a Participation that two Rankings of before
    /// ADR-0006 copy, a duplicate row, a person whose row is gone, and a name spelled two ways.
    /// </summary>
    static List<(string Collection, BsonDocument Document)> Spring()
    {
        var ivan = Person(
            1,
            "Иван Петров",
            "Ivan Petrov",
            fei: "100",
            country: Country("Bulgaria", "BG", "BUL"),
            club: "Sofia"
        );
        var ride70 = Participation(
            70,
            900,
            Combination(
                1,
                Person(1, "Иван Петров", fei: "100", country: Country("Bulgaria", "BGR"), club: "Sofia"),
                Person(20, "Burya")
            )
        );
        return
        [
            ("athletes", ivan),
            ("athletes", Person(2, "Nikolay Ivanov")),
            ("athletes", Person(3, "Nikolai Ivanov")),
            ("athletes", Person(4, "Maria Georgieva")),
            ("athletes", Person(5, "Maria Georgieva")),
            ("horses", Person(20, "Burya")),
            ("event_participations", ride70),
            (
                "event_participations",
                Participation(71, 901, Combination(2, Person(0, "IVAN PETROV"), Person(21, "Vihar")))
            ),
            (
                "event_participations",
                Participation(72, 900, Combination(3, Person(9, "Petar Dimitrov"), Person(22, "Zora")))
            ),
            (
                "event_participations",
                Participation(73, 900, Combination(4, Person(3, "Nikolai Ivanov"), Person(23, "Burya")))
            ),
            ("event_rankings", Ranking(300, 900, ride70)),
            ("event_rankings", Ranking(301, 900, ride70)), // the same Participation counted in a second Ranking
        ];
    }

    static string Report(IEnumerable<(string Collection, BsonDocument Document)> documents, string decisions = "{}")
    {
        var plan = IdentityPlan.Build(
            [.. documents.SelectMany(x => IdentityCopies.Read(x.Collection, x.Document))],
            EVENTS,
            IdentityDecisions.Parse(decisions)
        );
        return IdentityReport.Render(plan, "nts");
    }

    /// <summary>From a heading to the next heading of the same level or above.</summary>
    static string Section(string report, string heading)
    {
        var start = report.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The report has no \"{heading}\":\n{report}");
        var level = report[(report.LastIndexOf('\n', start) + 1)..].TakeWhile(x => x == '#').Count();
        var rest = report[(start + heading.Length)..];
        var end = rest.Length;
        foreach (var line in rest.Split('\n').Select((text, index) => (text, index)))
        {
            if (
                line.text.StartsWith(new string('#', level) + " ", StringComparison.Ordinal)
                || (level > 2 && line.text.StartsWith("## ", StringComparison.Ordinal))
            )
            {
                end = rest.IndexOf("\n" + line.text, StringComparison.Ordinal);
                break;
            }
        }

        return report[start..(start + heading.Length + Math.Max(end, 0))];
    }
}
