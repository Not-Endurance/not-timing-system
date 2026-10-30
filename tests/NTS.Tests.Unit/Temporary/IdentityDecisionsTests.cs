using System.Text.Json.Nodes;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: the decisions file the owner edits between two runs of <c>migrate-identities</c>.</summary>
public sealed class IdentityDecisionsTests
{
    const string KEY = "athlete: nikolai ivanov <> athlete: nikolay ivanov";

    [Fact]
    public void An_empty_file_decides_nothing()
    {
        Assert.Empty(IdentityDecisions.Parse("").ItemKeys);
        Assert.Empty(IdentityDecisions.Parse("  \n").ItemKeys);
        Assert.Empty(IdentityDecisions.Parse("{}").ItemKeys);
        Assert.Null(IdentityDecisions.Parse("{}").Rule(ReviewClass.NearMiss));
    }

    [Fact]
    public void An_item_is_same_or_different_in_any_case_and_a_blank_or_null_one_is_still_to_decide()
    {
        var decisions = IdentityDecisions.Parse(
            $$"""
            { "items": { "{{KEY}}": "Same", "b": "DIFFERENT", "c": null, "d": "", "e": " " } }
            """
        );

        Assert.Equal(IdentityDecision.Same, decisions.Item(KEY));
        Assert.Equal(IdentityDecision.Different, decisions.Item("b"));
        Assert.Null(decisions.Item("c"));
        Assert.Null(decisions.Item("d"));
        Assert.Null(decisions.Item("e"));
        Assert.Equal([KEY, "b"], decisions.ItemKeys.Order());
    }

    [Fact]
    public void A_rule_settles_a_class_and_is_named_as_the_class_is_in_any_case()
    {
        var decisions = IdentityDecisions.Parse(
            """
            { "rules": { "NearMiss": "different", "spellingvariant": "same", "Conflict": null } }
            """
        );

        Assert.Equal(IdentityDecision.Different, decisions.Rule(ReviewClass.NearMiss));
        Assert.Equal(IdentityDecision.Same, decisions.Rule(ReviewClass.SpellingVariant));
        Assert.Null(decisions.Rule(ReviewClass.Conflict));
        Assert.Null(decisions.Rule(ReviewClass.Held));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "items": [] }""")]
    [InlineData("""{ "rules": "same" }""")]
    [InlineData("""{ "rules": { "Mistyped": "same" } }""")]
    [InlineData("""{ "rules": { "NearMiss": "maybe" } }""")]
    [InlineData("""{ "items": { "a": "yes" } }""")]
    [InlineData("""{ "items": { "a": true } }""")]
    [InlineData("""{ "rules": { "99": "same" } }""")]
    [InlineData("""{ "rules": { "NearMiss": "same", "nearmiss": "different" } }""")]
    [InlineData("""{ "items": { "a": "same", "a": "different" } }""")]
    public void A_file_that_cannot_be_read_as_decisions_is_refused_and_not_guessed_at(string json)
    {
        Assert.Throws<FormatException>(() => IdentityDecisions.Parse(json));
    }

    [Fact]
    public void The_refusal_names_the_item_and_the_words_that_are_allowed()
    {
        var error = Assert.Throws<FormatException>(
            () => IdentityDecisions.Parse("""{ "items": { "athlete: a <> athlete: b": "yes" } }""")
        );

        Assert.Contains("athlete: a <> athlete: b", error.Message);
        Assert.Contains("same", error.Message);
        Assert.Contains("different", error.Message);
    }

    [Fact]
    public void The_template_lists_what_is_to_decide_beside_what_is_decided_and_every_class_of_rule()
    {
        var decisions = IdentityDecisions.Parse($$"""{ "items": { "{{KEY}}": "same" } }""");

        var written = JsonNode.Parse(
            decisions.ToJson(["athlete: zed <> athlete: zee", "athlete: abe <> athlete: abi"])
        )!;

        Assert.Equal(
            ["Held", "Conflict", "SpellingVariant", "NearMiss"],
            written["rules"]!.AsObject().Select(x => x.Key)
        );
        Assert.All(written["rules"]!.AsObject(), x => Assert.Null(x.Value));
        var items = written["items"]!.AsObject();
        Assert.Equal(["athlete: abe <> athlete: abi", KEY, "athlete: zed <> athlete: zee"], items.Select(x => x.Key)); // in order, so that the file is the same every run
        Assert.Equal("same", (string?)items[KEY]);
        Assert.Null(items["athlete: abe <> athlete: abi"]);
    }

    [Fact]
    public void A_comma_left_after_the_last_item_is_forgiven_because_the_owner_edits_the_file_by_hand()
    {
        var decisions = IdentityDecisions.Parse("""{ "items": { "a": "same", }, }""");

        Assert.Equal(IdentityDecision.Same, decisions.Item("a"));
    }

    [Fact]
    public void The_file_is_written_indented_so_that_a_person_can_read_and_edit_it()
    {
        var written = IdentityDecisions.None.ToJson(["athlete: a <> athlete: b"]);

        Assert.Contains("\n  \"rules\": {\n    \"Held\": null,", written);
        Assert.Contains("\n  \"items\": {\n    \"athlete: a <> athlete: b\": null\n  }", written);
    }

    [Fact]
    public void An_item_the_file_already_has_is_not_written_again_over_what_the_owner_decided()
    {
        var decisions = IdentityDecisions.Parse("""{ "items": { "x": "Different" } }""");

        var written = JsonNode.Parse(decisions.ToJson(["x", "y"]))!;

        Assert.Equal("Different", (string?)written["items"]!["x"]);
        Assert.Null(written["items"]!["y"]);
    }

    [Fact]
    public void The_template_keeps_what_the_owner_wrote_and_what_it_does_not_know()
    {
        var decisions = IdentityDecisions.Parse(
            """
            { "note": "asked Petar", "rules": { "nearmiss": "different" }, "items": { "x": "Different" } }
            """
        );

        var written = JsonNode.Parse(decisions.ToJson(["y"]))!;

        Assert.Equal("asked Petar", (string?)written["note"]);
        Assert.Equal("different", ((string?)written["rules"]!["nearmiss"])?.ToLowerInvariant());
        Assert.Single(written["rules"]!.AsObject(), x => x.Key.Equals("nearmiss", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Different", (string?)written["items"]!["x"]);
        Assert.True(written["items"]!.AsObject().ContainsKey("y"));
    }

    [Fact]
    public void Writing_the_template_again_from_what_it_wrote_changes_nothing()
    {
        var first = IdentityDecisions.None.ToJson(["athlete: b <> athlete: c", "athlete: a <> athlete: d"]);

        var second = IdentityDecisions.Parse(first).ToJson(["athlete: b <> athlete: c", "athlete: a <> athlete: d"]);

        Assert.Equal(first, second);
        Assert.EndsWith("\n", first);
        Assert.DoesNotContain("\r", first);
    }

    [Fact]
    public void Names_are_written_as_they_are_and_not_as_escapes()
    {
        var written = IdentityDecisions.None.ToJson(["athlete: иван петров <> athlete: ivan petrov"]);

        Assert.Contains("athlete: иван петров <> athlete: ivan petrov", written);
    }

    [Fact]
    public void The_fingerprint_is_of_what_is_decided_and_of_nothing_else()
    {
        var one = IdentityDecisions.Parse(
            $$"""{ "rules": { "NearMiss": "different" }, "items": { "{{KEY}}": "same", "b": "different" } }"""
        );
        var reordered = IdentityDecisions.Parse(
            $$"""{ "items": { "b": "different", "{{KEY}}": "SAME" }, "rules": { "NearMiss": "Different" }, "notes": 3 }"""
        );
        var undecided = IdentityDecisions.Parse(
            $$"""{ "rules": { "NearMiss": "different" }, "items": { "{{KEY}}": "same", "b": null } }"""
        );
        var other = IdentityDecisions.Parse(
            $$"""{ "rules": { "NearMiss": "same" }, "items": { "{{KEY}}": "same", "b": "different" } }"""
        );
        var another = IdentityDecisions.Parse(
            $$"""{ "rules": { "NearMiss": "different" }, "items": { "{{KEY}}": "different", "b": "different" } }"""
        );

        Assert.Equal(one.Fingerprint(), reordered.Fingerprint());
        Assert.NotEqual(one.Fingerprint(), undecided.Fingerprint());
        Assert.NotEqual(one.Fingerprint(), other.Fingerprint());
        Assert.NotEqual(one.Fingerprint(), another.Fingerprint());
        Assert.Equal(IdentityDecisions.None.Fingerprint(), IdentityDecisions.Parse("").Fingerprint());
    }
}
