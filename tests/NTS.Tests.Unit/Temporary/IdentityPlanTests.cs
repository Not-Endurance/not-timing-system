using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.IdentityMigration;
using static NTS.Tests.Unit.Temporary.IdentityMigrationDocuments;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: what <c>migrate-identities</c> makes of the copies it found, before any decision is taken.</summary>
public sealed class IdentityPlanTests
{
    static readonly BsonDocument HORSE = Person(20, "Burya");

    [Fact]
    public void Copies_that_say_the_same_are_one_spelling_and_a_Setup_row_is_never_collapsed_into_them()
    {
        var plan = Plan(
            ("athletes", Person(1, "Ivan Petrov", fei: "100")),
            Ride(70, 900, Person(1, "Ivan Petrov", fei: "100")),
            Ride(71, 901, Person(1, "Ivan Petrov", fei: "100"))
        );

        var athletes = plan.Athletes;
        Assert.Equal(3, athletes.Copies.Count);
        Assert.Equal(2, athletes.Spellings.Count);
        Assert.Equal([1, 2], athletes.Spellings.Select(x => x.Copies.Count).Order());
        Assert.Single(athletes.Spellings, x => x.IsSetupRow);
        Assert.Single(athletes.Identities);
    }

    [Fact]
    public void Two_Setup_rows_that_say_the_same_stay_two_rows_and_the_second_is_a_duplicate_to_merge()
    {
        var plan = Plan(("athletes", Person(1, "Ivan Petrov")), ("athletes", Person(2, "Ivan Petrov")));

        var athletes = plan.Athletes;
        Assert.Equal(2, athletes.SetupRows);
        var identity = Assert.Single(athletes.Identities);
        Assert.Equal(2, identity.SetupRows);
        Assert.Equal(1, athletes.DuplicateRows);
        Assert.Equal(0, athletes.RowsToCreate);
    }

    [Fact]
    public void Two_Setup_rows_with_Guid_ids_that_say_the_same_are_two_rows_all_the_same()
    {
        var plan = Plan(
            ("athletes", Person(new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), "Ivan Petrov")),
            ("athletes", Person(new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), "Ivan Petrov"))
        );

        var athletes = plan.Athletes;
        Assert.Equal(2, athletes.Spellings.Count);
        Assert.Equal(2, athletes.SetupRows);
        Assert.Equal(1, athletes.DuplicateRows);
    }

    [Theory]
    [InlineData("Ivan Petrov", "100", null, "Petrov Ivan", "100", null, MatchClass.SameFeiId)]
    [InlineData("Ivan Petrov", null, null, "IVAN  PETROV", null, null, MatchClass.ExactName)]
    [InlineData("Иван Петров", null, null, "Ivan Petrov", null, null, MatchClass.StrictTransliteration)]
    [InlineData("Maria Georgieva", null, "42", "Maria Georgiev", null, "42", MatchClass.SameLegacyId)]
    public void Each_automatic_rule_joins_two_spellings_and_the_group_says_which_rule_did(
        string name,
        string? fei,
        string? legacy,
        string another,
        string? anotherFei,
        string? anotherLegacy,
        MatchClass expected
    )
    {
        var plan = Plan(
            Ride(70, 900, Person(legacy == null ? 0 : int.Parse(legacy), name, fei: fei)),
            Ride(71, 901, Person(anotherLegacy == null ? 0 : int.Parse(anotherLegacy), another, fei: anotherFei))
        );

        var group = Assert.Single(plan.Athletes.Groups);
        Assert.Equal(2, group.Spellings.Count);
        var link = Assert.Single(group.Links);
        Assert.Equal(expected, link.Class);
        Assert.Empty(plan.Athletes.Entries);
        Assert.Single(plan.Athletes.Identities);
    }

    [Fact]
    public void A_Setup_row_with_both_scripts_joins_a_copy_in_Cyrillic_only_and_a_copy_in_Latin_only()
    {
        var plan = Plan(
            ("athletes", Person(1, "Иван Петров", "Ivan Petrov")),
            Ride(70, 900, Person(0, "Иван Петров")),
            Ride(71, 901, Person(0, "IVAN PETROV"))
        );

        var identity = Assert.Single(plan.Athletes.Identities);
        Assert.Equal(3, identity.Spellings.Count);
        Assert.Equal(1, identity.SetupRows);
    }

    [Fact]
    public void The_id_0_joins_nobody()
    {
        var plan = Plan(
            Ride(70, 900, Person(0, "Ivan Petrov")),
            Ride(71, 901, Person(0, "Maria Georgieva")),
            Ride(72, 902, Person(0, "Petar Dimitrov"))
        );

        Assert.Equal(3, plan.Athletes.Identities.Count);
        Assert.Equal(0, plan.Athletes.DistinctLegacyIds);
        Assert.Empty(plan.Athletes.Entries);
    }

    [Fact]
    public void The_counts_tell_the_rows_that_exist_the_rows_to_create_the_duplicates_and_the_copies_whose_row_is_gone()
    {
        var plan = Plan(
            ("athletes", Person(1, "Ivan Petrov", fei: "100")),
            ("athletes", Person(2, "Ivan Petrov", fei: "100")), // a duplicate of the first
            ("athletes", Person(3, "Maria Georgieva")),
            Ride(70, 900, Person(1, "Ivan Petrov", fei: "100")),
            Ride(71, 900, Person(3, "Maria Georgieva")),
            Ride(72, 900, Person(9, "Petar Dimitrov")), // no Setup row
            Ride(73, 901, Person(9, "Petar Dimitrov"))
        );

        var athletes = plan.Athletes;
        Assert.Equal(7, athletes.Copies.Count);
        Assert.Equal(6, athletes.Spellings.Count);
        Assert.Equal(4, athletes.DistinctLegacyIds); // 1, 2, 3 and 9
        Assert.Equal(3, athletes.Identities.Count);
        Assert.Equal(3, athletes.SetupRows);
        Assert.Equal(1, athletes.RowsToCreate);
        Assert.Equal(1, athletes.DuplicateRows);
        Assert.Equal(2, athletes.CopiesWithoutRow);
    }

    [Fact]
    public void Athletes_and_Horses_are_planned_apart_and_a_Horse_may_share_a_name_with_an_Athlete()
    {
        var plan = Plan(
            ("athletes", Person(1, "Zora")),
            ("horses", Person(1, "Zora")),
            Ride(70, 900, Person(2, "Ivan Petrov"), Person(3, "Zora"))
        );

        Assert.Equal(2, plan.Athletes.Identities.Count);
        Assert.Single(plan.Horses.Identities);
        Assert.Equal(2, plan.Horses.Copies.Count(x => !x.IsSetupRow || x.IsSetupRow));
    }

    [Fact]
    public void A_spelling_variant_a_near_miss_and_a_conflict_are_for_review_and_join_nothing()
    {
        var plan = Plan(
            ("athletes", Person(1, "Nikolay Ivanov")),
            ("athletes", Person(2, "Nikolai Ivanov")), // the same letters, spelled otherwise
            ("athletes", Person(3, "Maria Georgieva")),
            ("athletes", Person(4, "Maria Georgiev")), // one letter apart
            ("athletes", Person(5, "Stefan Kolev", fei: "100")),
            ("athletes", Person(6, "Stefan Kolev", fei: "200")) // equal names, two FEI IDs
        );

        var athletes = plan.Athletes;
        Assert.Equal(6, athletes.Identities.Count);
        Assert.Equal(
            [ReviewClass.Conflict, ReviewClass.SpellingVariant, ReviewClass.NearMiss],
            athletes.Entries.Select(x => x.Class)
        );
        Assert.All(athletes.Entries, x => Assert.Null(x.Decision));
        Assert.All(athletes.Entries, x => Assert.False(x.ByRule)); // nothing is decided by a rule that is not there
        Assert.Equal(3, athletes.ToReview);
    }

    [Fact]
    public void The_key_of_a_group_has_the_names_in_English_as_well_as_the_names()
    {
        var plan = Plan(("athletes", Person(1, "Иван Петров", "Ivan Petrov")));

        Assert.Equal("athlete: ivan petrov | иван петров", Assert.Single(plan.Athletes.Groups).Key);
    }

    [Fact]
    public void The_entries_to_review_are_in_the_order_of_their_keys_within_a_class()
    {
        var plan = Plan(
            ("athletes", Person(1, "Zed Nikolay")),
            ("athletes", Person(2, "Zed Nikolai")),
            ("athletes", Person(3, "Abe Nikolay")),
            ("athletes", Person(4, "Abe Nikolai"))
        );

        Assert.Equal(
            ["athlete: abe nikolai <> athlete: abe nikolay", "athlete: zed nikolai <> athlete: zed nikolay"],
            plan.Athletes.Entries.Select(x => x.Key)
        );
    }

    [Fact]
    public void A_group_that_a_chain_bridges_over_a_conflict_is_held_whole()
    {
        var plan = Plan(
            ("athletes", Person(1, "Stefan Kolev", fei: "100")),
            ("athletes", Person(2, "Stefan Kolev", fei: "200")),
            ("athletes", Person(3, "Stefan Kolev"))
        );

        var athletes = plan.Athletes;
        var held = Assert.Single(athletes.Entries);
        Assert.Equal(ReviewClass.Held, held.Class);
        Assert.Equal(3, held.Left.Spellings.Count);
        Assert.Null(held.Right);
        Assert.Equal(3, athletes.Identities.Count); // nothing in it merges by itself
        Assert.Equal(1, athletes.ToReview);
    }

    [Fact]
    public void A_copy_with_no_name_is_set_aside_and_never_matched()
    {
        var plan = Plan(("athletes", Person(1, null)), ("athletes", Person(2, "Ivan Petrov")));

        var athletes = plan.Athletes;
        Assert.Equal(2, athletes.Copies.Count);
        var unreadable = Assert.Single(athletes.Unreadable);
        Assert.Equal("1", unreadable.DocumentId);
        Assert.Single(athletes.Spellings);
    }

    [Fact]
    public void The_key_of_an_item_is_made_of_the_normalised_spellings_and_the_FEI_ID_and_of_no_database_id()
    {
        var plan = Plan(
            ("athletes", Person(1, "Stefan  KOLEV", "Stefan Kolev", fei: "00100")),
            ("athletes", Person(2, "Стефан Колев", fei: "200")),
            ("athletes", Person(3, "Stefan Kolev", fei: "200")),
            ("horses", Person(9, "Burya"))
        );

        var conflict = Assert.Single(plan.Athletes.Entries);
        Assert.Equal("athlete (fei 100): stefan kolev <> athlete (fei 200): stefan kolev | стефан колев", conflict.Key);
        Assert.Equal("athlete (fei 100): stefan kolev", conflict.Left.Key);
        Assert.Equal("horse: burya", Assert.Single(plan.Horses.Groups).Key);
    }

    [Fact]
    public void The_same_copies_in_any_order_make_the_same_plan()
    {
        var documents = new (string, BsonDocument)[]
        {
            ("athletes", Person(1, "Иван Петров", "Ivan Petrov", fei: "100")),
            ("athletes", Person(2, "Ivan Petrov", fei: "100")),
            ("athletes", Person(3, "Nikolay Ivanov")),
            ("athletes", Person(4, "Nikolai Ivanov")),
            Ride(70, 900, Person(0, "Иван Петров")),
            Ride(71, 901, Person(5, "Maria Georgieva")),
            Ride(72, 901, Person(6, "Maria Georgiev")),
        };
        var forward = Plan(documents);
        var backward = Plan([.. documents.Reverse()]);

        Assert.Equal(Describe(forward), Describe(backward));
    }

    [Fact]
    public void Copies_that_differ_in_the_name_in_English_the_legacy_id_or_the_FEI_ID_are_spellings_apart()
    {
        var plan = Plan(
            Ride(70, 900, Person(5, "Ivan Petrov", "Ivan Petrov", fei: "100")),
            Ride(71, 900, Person(5, "Ivan Petrov", "Ivan Petrov", fei: "100")), // the same as the one above
            Ride(72, 900, Person(5, "Ivan Petrov", "Ivan P.", fei: "100")),
            Ride(73, 900, Person(6, "Ivan Petrov", "Ivan Petrov", fei: "100")),
            Ride(74, 900, Person(5, "Ivan Petrov", "Ivan Petrov"))
        );

        var athletes = plan.Athletes;
        Assert.Equal(5, athletes.Copies.Count);
        Assert.Equal(4, athletes.Spellings.Count);
        Assert.Equal([1, 1, 1, 2], athletes.Spellings.Select(x => x.Copies.Count).Order());
    }

    [Fact]
    public void The_FEI_IDs_in_the_key_of_a_held_group_are_in_order_whichever_row_has_which()
    {
        var plan = Plan(
            ("athletes", Person(1, "Stefan Kolev", fei: "200")),
            ("athletes", Person(2, "Stefan Kolev", fei: "100")),
            ("athletes", Person(3, "Stefan Kolev"))
        );

        Assert.Equal("athlete (fei 100, 200): stefan kolev", Assert.Single(plan.Athletes.Entries).Key);
    }

    [Fact]
    public void The_names_in_the_key_of_a_group_are_in_order_whichever_spelling_comes_first()
    {
        var plan = Plan(
            ("athletes", Person(1, "Стефан Колев", "Stefan Kolev")),
            ("athletes", Person(2, "Stefan Kolev"))
        );

        Assert.Equal("athlete: stefan kolev | стефан колев", Assert.Single(plan.Athletes.Groups).Key);
    }

    static IdentityPlan Plan(params (string Collection, BsonDocument Document)[] documents)
    {
        return IdentityPlan.Build(
            [.. documents.SelectMany(x => IdentityCopies.Read(x.Collection, x.Document))],
            new Dictionary<string, string>(),
            IdentityDecisions.None
        );
    }

    /// <summary>A ride of an Athlete in an Event that has started.</summary>
    static (string, BsonDocument) Ride(int id, int eventId, BsonDocument athlete, BsonDocument? horse = null)
    {
        return ("event_participations", Participation(id, eventId, Combination(id, athlete, horse ?? HORSE)));
    }

    static string Describe(IdentityPlan plan)
    {
        return string.Join(
            "\n",
            plan.Kinds.SelectMany(kind =>
                kind.Groups.Select(x => $"group {x.Key}: {string.Join(" + ", x.Spellings.Select(y => y.Key))}")
                    .Concat(kind.Entries.Select(x => $"entry {x.Key} {x.Class}"))
                    .Concat(
                        kind.Identities.Select(x => $"identity {string.Join(" + ", x.Spellings.Select(y => y.Key))}")
                    )
            )
        );
    }
}
