using NTS.Tools.IdentityMigration;
using static NTS.Tests.Unit.Temporary.IdentityMigrationDocuments;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: what the owner's decisions do to the plan, and what they must not.</summary>
public sealed class IdentityPlanDecisionsTests
{
    const string VARIANT = "athlete: nikolai ivanov <> athlete: nikolay ivanov";
    const string NEAR_MISS = "athlete: maria georgiev <> athlete: maria georgieva";
    const string CONFLICT = "athlete (fei 100): stefan kolev <> athlete (fei 200): stefan kolev";

    [Fact]
    public void A_decision_of_same_joins_the_two_groups_of_the_item_and_nothing_else()
    {
        var plan = Plan($$"""{ "items": { "{{VARIANT}}": "same" } }""", Pairs());

        var athletes = plan.Athletes;
        Assert.Equal(5, athletes.Identities.Count);
        var joined = Assert.Single(athletes.Identities, x => x.Spellings.Count == 2 && x.ByDecision);
        Assert.Equal(2, joined.SetupRows);
        Assert.Equal(1, athletes.DuplicateRows);
        var entry = athletes.Entries.Single(x => x.Key == VARIANT);
        Assert.Equal(IdentityDecision.Same, entry.Decision);
        Assert.True(entry.IsApplied);
        Assert.False(entry.ByRule);
        Assert.Equal(2, athletes.ToReview);
        Assert.Equal(1, athletes.Decided);
    }

    [Fact]
    public void A_decision_of_different_settles_the_item_and_keeps_the_two_apart()
    {
        var plan = Plan($$"""{ "items": { "{{CONFLICT}}": "different" } }""", Pairs());

        var athletes = plan.Athletes;
        Assert.Equal(6, athletes.Identities.Count);
        Assert.Equal(IdentityDecision.Different, athletes.Entries.Single(x => x.Key == CONFLICT).Decision);
        Assert.Equal(2, athletes.ToReview);
        Assert.Empty(athletes.Contradictions);
    }

    [Fact]
    public void A_rule_settles_every_item_of_its_class_and_an_item_of_its_own_beats_the_rule()
    {
        var plan = Plan(
            $$"""
            { "rules": { "SpellingVariant": "same", "NearMiss": "different" }, "items": { "{{NEAR_MISS}}": "same" } }
            """,
            Pairs()
        );

        var athletes = plan.Athletes;
        var variant = athletes.Entries.Single(x => x.Key == VARIANT);
        Assert.Equal(IdentityDecision.Same, variant.Decision);
        Assert.True(variant.ByRule);
        var nearMiss = athletes.Entries.Single(x => x.Key == NEAR_MISS);
        Assert.Equal(IdentityDecision.Same, nearMiss.Decision); // its own word, not the rule's
        Assert.False(nearMiss.ByRule);
        Assert.Null(athletes.Entries.Single(x => x.Key == CONFLICT).Decision); // no rule for its class
        Assert.Equal(4, athletes.Identities.Count);
        Assert.Equal(1, athletes.ToReview);
    }

    [Fact]
    public void A_held_group_is_one_when_the_owner_says_so_and_stays_apart_when_they_say_different()
    {
        var rows = new[]
        {
            Person(1, "Stefan Kolev", fei: "100"),
            Person(2, "Stefan Kolev", fei: "200"),
            Person(3, "Stefan Kolev"),
        };
        const string HELD = "athlete (fei 100, 200): stefan kolev";

        var undecided = Plan("{}", rows).Athletes;
        var same = Plan($$"""{ "items": { "{{HELD}}": "same" } }""", rows).Athletes;
        var different = Plan($$"""{ "items": { "{{HELD}}": "different" } }""", rows).Athletes;

        Assert.Equal(3, undecided.Identities.Count);
        Assert.Equal(1, undecided.ToReview);
        Assert.Single(same.Identities);
        Assert.True(same.Entries.Single().IsApplied);
        Assert.Equal(0, same.ToReview);
        Assert.Equal(3, different.Identities.Count);
        Assert.Equal(0, different.ToReview);
    }

    [Fact]
    public void A_decision_of_same_next_to_a_held_group_waits_until_the_held_group_is_decided()
    {
        var rows = new[]
        {
            Person(1, "Stefan Kolev", fei: "100"),
            Person(2, "Stefan Kolev", fei: "200"),
            Person(3, "Stefan Kolev"),
            Person(4, "Stephan Kolev"),
        };
        const string HELD = "athlete (fei 100, 200): stefan kolev";
        const string NEXT_TO = "athlete (fei 100, 200): stefan kolev <> athlete: stephan kolev";

        var waiting = Plan($$"""{ "items": { "{{NEXT_TO}}": "same" } }""", rows).Athletes;
        var decided = Plan($$"""{ "items": { "{{NEXT_TO}}": "same", "{{HELD}}": "same" } }""", rows).Athletes;

        Assert.Equal(4, waiting.Identities.Count);
        Assert.False(waiting.Entries.Single(x => x.Key == NEXT_TO).IsApplied);
        Assert.Equal(1, waiting.Waiting);
        Assert.Single(decided.Identities);
        Assert.True(decided.Entries.Single(x => x.Key == NEXT_TO).IsApplied);
        Assert.Equal(0, decided.Waiting);
    }

    [Fact]
    public void A_decision_of_same_waits_for_a_held_group_that_is_the_second_of_the_pair_as_well()
    {
        var rows = new[]
        {
            Person(1, "Stephan Kolev"), // the first group of the pair, which is not held
            Person(2, "Stefan Kolev", fei: "100"),
            Person(3, "Stefan Kolev", fei: "200"),
            Person(4, "Stefan Kolev"),
        };
        const string NEXT_TO = "athlete (fei 100, 200): stefan kolev <> athlete: stephan kolev";

        var waiting = Plan($$"""{ "items": { "{{NEXT_TO}}": "same" } }""", rows).Athletes;

        Assert.Equal(4, waiting.Identities.Count);
        Assert.Equal(1, waiting.Waiting);
    }

    [Fact]
    public void The_spellings_of_an_identity_that_decisions_joined_are_in_the_order_of_their_keys()
    {
        var rows = new[] { Person(1, "Nikolay Ivanov"), Person(5, "Nikolay Ivanov"), Person(3, "Nikolai Ivanov") };
        const string KEY = "athlete: nikolai ivanov <> athlete: nikolay ivanov";

        var plan = Plan($$"""{ "items": { "{{KEY}}": "same" } }""", rows).Athletes;

        var identity = Assert.Single(plan.Identities);
        Assert.Equal(["1", "3", "5"], identity.Spellings.Select(x => x.Copies[0].DocumentId));
    }

    [Fact]
    public void Decisions_that_name_nothing_are_listed_in_order_whichever_the_file_has_first()
    {
        var plan = Plan(
            """{ "items": { "athlete: z <> athlete: zz": "same", "athlete: a <> athlete: aa": "different" } }""",
            Pairs()
        );

        Assert.Equal(["athlete: a <> athlete: aa", "athlete: z <> athlete: zz"], plan.UnusedDecisions);
    }

    [Fact]
    public void Decisions_that_name_no_item_of_the_data_are_listed_and_a_blank_one_is_not()
    {
        var plan = Plan(
            $$"""{ "items": { "{{VARIANT}}": "same", "athlete: a <> athlete: b": "same", "athlete: c <> athlete: d": null } }""",
            Pairs()
        );

        Assert.Equal(["athlete: a <> athlete: b"], plan.UnusedDecisions);
    }

    [Fact]
    public void A_pair_decided_different_that_other_decisions_join_all_the_same_is_a_contradiction()
    {
        var rows = new[] { Person(1, "Nikolay Ivanov"), Person(2, "Nikolai Ivanov"), Person(3, "Nikolaj Ivanov") };
        const string AB = "athlete: nikolai ivanov <> athlete: nikolay ivanov";
        const string AC = "athlete: nikolaj ivanov <> athlete: nikolay ivanov";
        const string BC = "athlete: nikolai ivanov <> athlete: nikolaj ivanov";

        var plan = Plan(
            $$"""{ "items": { "{{AB}}": "same", "{{BC}}": "same", "{{AC}}": "different" } }""",
            rows
        ).Athletes;

        Assert.Single(plan.Identities);
        var contradiction = Assert.Single(plan.Contradictions);
        Assert.Equal(AC, contradiction.Key);
    }

    [Fact]
    public void A_pair_that_a_rule_keeps_apart_is_no_contradiction_when_others_join_it()
    {
        var rows = new[] { Person(1, "Nikolay Ivanov"), Person(2, "Nikolai Ivanov"), Person(3, "Nikolaj Ivanov") };
        const string AB = "athlete: nikolai ivanov <> athlete: nikolay ivanov";
        const string BC = "athlete: nikolai ivanov <> athlete: nikolaj ivanov";

        var plan = Plan(
            $$"""{ "rules": { "SpellingVariant": "different" }, "items": { "{{AB}}": "same", "{{BC}}": "same" } }""",
            rows
        ).Athletes;

        Assert.Single(plan.Identities);
        Assert.Empty(plan.Contradictions);
    }

    static IdentityPlan Plan(string decisions, params MongoDB.Bson.BsonDocument[] rows)
    {
        return IdentityPlan.Build(
            [.. rows.SelectMany(x => IdentityCopies.Read("athletes", x))],
            new Dictionary<string, string>(),
            IdentityDecisions.Parse(decisions)
        );
    }

    static MongoDB.Bson.BsonDocument[] Pairs()
    {
        return
        [
            Person(1, "Nikolay Ivanov"),
            Person(2, "Nikolai Ivanov"),
            Person(3, "Maria Georgieva"),
            Person(4, "Maria Georgiev"),
            Person(5, "Stefan Kolev", fei: "100"),
            Person(6, "Stefan Kolev", fei: "200"),
        ];
    }
}
