using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Setup.Services.Identities;

/// <summary>
/// The copies of Athletes and Horses sorted into the ones that are one (#635, ADR-0010): what is matched automatically is joined
/// into a group through any chain of copies, a group in which two copies disagree is held whole, and what is only a spelling
/// variant, a near-miss or a conflict between groups is left for a person to review. The names are made up.
/// </summary>
public sealed class IdentityGroupingTests
{
    [Fact]
    public void A_Setup_row_that_has_both_scripts_joins_a_copy_in_Cyrillic_only_and_a_copy_in_Latin_only()
    {
        var cyrillic = Rider("copy-cyrillic", "Юлия Тестова");
        var latin = Rider("copy-latin", "Iuliia Testova");
        var row = Rider("setup-row", "Юлия Тестова", "Iuliia Testova");

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [cyrillic, latin, row]);

        var group = Assert.Single(grouping.Groups);
        Assert.Equal(["copy-cyrillic", "copy-latin", "setup-row"], KeysOf(group));
        Assert.False(group.IsHeld);
        Assert.Equal([MatchClass.ExactName], group.Classes); // the two copies are one only through the row
        Assert.Empty(grouping.Review);
    }

    [Fact]
    public void The_same_two_copies_without_the_row_are_two_identities_and_a_review_item()
    {
        var cyrillic = Rider("copy-cyrillic", "Юлия Тестова");
        var latin = Rider("copy-latin", "Iuliia Testova");

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [cyrillic, latin]);

        Assert.Equal(2, grouping.Groups.Count);
        var item = Assert.Single(grouping.Review);
        Assert.Equal(MatchClass.SpellingVariant, item.Class);
        Assert.Equal(["copy-cyrillic"], KeysOf(item.Left));
        Assert.Equal(["copy-latin"], KeysOf(item.Right));
    }

    [Fact]
    public void Copies_are_one_through_a_chain_and_the_group_says_which_classes_joined_it()
    {
        var first = Rider("first", "Мариела Тестова");
        var second = Rider("second", "МАРИЕЛА ТЕСТОВА", feiId: "10012345");
        var third = Rider("third", "Mariela Testova", feiId: "10012345");
        var alone = Rider("alone", "Борис Примеров");

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [first, second, third, alone]);

        Assert.Equal(2, grouping.Groups.Count);
        var group = grouping.Groups[0];
        Assert.Equal(["first", "second", "third"], KeysOf(group));
        Assert.Equal([MatchClass.SameFeiId, MatchClass.ExactName, MatchClass.StrictTransliteration], group.Classes);
        Assert.Contains(group.Links, x => x.Class == MatchClass.ExactName && Pair(x, "first", "second"));
        Assert.Contains(group.Links, x => x.Class == MatchClass.SameFeiId && Pair(x, "second", "third"));
        Assert.Contains(group.Links, x => x.Class == MatchClass.StrictTransliteration && Pair(x, "first", "third"));
        Assert.Equal(["alone"], KeysOf(grouping.Groups[1]));
        Assert.Empty(grouping.Groups[1].Links);
        Assert.Empty(grouping.Review);
    }

    [Fact]
    public void A_group_in_which_two_copies_disagree_is_held_whole()
    {
        var one = Rider("one", "Мариела Тестова", feiId: "10012345");
        var another = Rider("another", "Мариела Тестова", feiId: "10012346");
        var between = Rider("between", "Мариела Тестова"); // equal to both, so it would join them

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [one, another, between]);

        var group = Assert.Single(grouping.Groups);
        Assert.True(group.IsHeld);
        var conflict = Assert.Single(group.Conflicts);
        Assert.Equal(MatchClass.Conflict, conflict.Class);
        Assert.True(Pair(conflict, "one", "another"));
        Assert.Equal(3, group.Members.Count);
        Assert.Empty(grouping.Review); // what disagrees inside a group belongs to the group, and is no pair of groups
    }

    [Fact]
    public void Two_copies_that_disagree_and_nothing_joins_are_two_groups_and_a_conflict_to_review()
    {
        var one = Rider("one", "Мариела Тестова", feiId: "10012345");
        var another = Rider("another", "Мариела Тестова", feiId: "10012346");

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [one, another]);

        Assert.Equal(2, grouping.Groups.Count);
        Assert.All(grouping.Groups, x => Assert.False(x.IsHeld));
        var item = Assert.Single(grouping.Review);
        Assert.Equal(MatchClass.Conflict, item.Class);
    }

    [Fact]
    public void A_near_miss_between_two_groups_is_one_review_item_that_lists_the_copies_that_were_alike()
    {
        var first = Rider("first", "Ivan Testov", feiId: "10012345");
        var again = Rider("again", "ivan testov");
        var close = Rider("close", "Ivan Testoff");

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [first, again, close]);

        Assert.Equal(2, grouping.Groups.Count);
        var item = Assert.Single(grouping.Review);
        Assert.Equal(MatchClass.NearMiss, item.Class);
        Assert.Equal(["first", "again"], KeysOf(item.Left));
        Assert.Equal(["close"], KeysOf(item.Right));
        Assert.Equal(2, item.Links.Count); // each of the two copies is close to the third
        Assert.All(item.Links, x => Assert.Equal(MatchClass.NearMiss, x.Class));
    }

    [Fact]
    public void A_review_item_is_told_by_the_strongest_class_between_its_groups()
    {
        var p = Rider("p", "Юлия Тестова");
        var q = Rider("q", "ЮЛИЯ ТЕСТОВА", feiId: "10012345");
        var r = Rider("r", "Iuliia Testova");
        var s = Rider("s", "iuliia testova", feiId: "10012346");

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [p, q, r, s]);

        Assert.Equal(2, grouping.Groups.Count);
        var item = Assert.Single(grouping.Review);
        Assert.Equal(MatchClass.Conflict, item.Class); // q and s are one name under two FEI IDs, and the rest only spellings of it
        Assert.Equal(4, item.Links.Count);
        Assert.Equal(3, item.Links.Count(x => x.Class == MatchClass.SpellingVariant));
    }

    [Fact]
    public void A_spelling_variant_outweighs_a_near_miss_between_the_same_two_groups()
    {
        var a = Rider("a", "Юлия Тестова");
        var b = Rider("b", "Iuliia Testova", feiId: "10012345");
        var c = Rider("c", "Iuliia Testov", feiId: "10012345"); // joins b by the FEI ID, and is only a near-miss of a

        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [a, b, c]);

        Assert.Equal(2, grouping.Groups.Count);
        var item = Assert.Single(grouping.Review);
        Assert.Equal(MatchClass.SpellingVariant, item.Class);
        Assert.Equal([MatchClass.SpellingVariant, MatchClass.NearMiss], item.Links.Select(x => x.Class));
    }

    [Fact]
    public void The_review_items_are_in_the_order_of_their_groups()
    {
        var grouping = IdentityMatcher.Group(
            IdentityKind.Athlete,
            [
                Rider("p", "Юлия Тестова"),
                Rider("q", "Iuliia Testova"),
                Rider("r", "Яна Примерова"),
                Rider("s", "Iana Primerova"),
            ]
        );

        Assert.Equal(4, grouping.Groups.Count);
        Assert.Equal(
            [("p", "q"), ("r", "s")],
            grouping.Review.Select(x => (x.Left.Members[0].Key, x.Right.Members[0].Key))
        );
    }

    [Fact]
    public void Copies_with_the_legacy_id_0_and_names_that_differ_are_not_joined_or_reviewed()
    {
        var grouping = IdentityMatcher.Group(
            IdentityKind.Athlete,
            [
                Rider("one", "Мариела Тестова", legacyId: "0"),
                Rider("two", "Борис Примеров", legacyId: "0"),
                Rider("three", "Ясен Проверов", legacyId: "0"),
            ]
        );

        Assert.Equal(3, grouping.Groups.Count);
        Assert.Empty(grouping.Review);
        Assert.All(grouping.Groups, x => Assert.Empty(x.Links));
    }

    [Fact]
    public void A_legacy_id_that_two_copies_share_joins_them_and_the_group_says_so()
    {
        var grouping = IdentityMatcher.Group(
            IdentityKind.Athlete,
            [Rider("one", "Ivan Testov", legacyId: "17"), Rider("two", "Ivan Testoff", legacyId: "17")]
        );

        var group = Assert.Single(grouping.Groups);
        Assert.Equal([MatchClass.SameLegacyId], group.Classes);
    }

    [Fact]
    public void Horses_with_the_same_name_are_one_group()
    {
        var grouping = IdentityMatcher.Group(
            IdentityKind.Horse,
            [
                Rider("one", "Тест Огън"),
                Rider("two", " тест  огън"),
                Rider("three", "Test Ogan"),
                Rider("four", "Друг Кон"),
            ]
        );

        Assert.Equal(2, grouping.Groups.Count);
        Assert.Equal(["one", "two", "three"], KeysOf(grouping.Groups[0]));
        Assert.Equal([MatchClass.ExactName, MatchClass.StrictTransliteration], grouping.Groups[0].Classes);
    }

    [Fact]
    public void The_groups_are_in_the_order_their_first_copy_came_in_and_the_same_copies_give_the_same_groups()
    {
        var copies = new[]
        {
            Rider("a", "Борис Примеров"),
            Rider("b", "Мариела Тестова"),
            Rider("c", "борис примеров"),
            Rider("d", "Ясен Проверов"),
            Rider("e", "Mariela Testova"),
        };

        var first = IdentityMatcher.Group(IdentityKind.Athlete, copies);
        var second = IdentityMatcher.Group(IdentityKind.Athlete, copies);

        Assert.Equal([new[] { "a", "c" }, ["b", "e"], ["d"]], first.Groups.Select(KeysOf).ToArray());
        Assert.Equal(first.Groups.Select(KeysOf), second.Groups.Select(KeysOf));
        Assert.Equal(first.Review.Count, second.Review.Count);
    }

    [Fact]
    public void Every_copy_is_in_one_group_even_when_it_matches_nothing()
    {
        var grouping = IdentityMatcher.Group(IdentityKind.Athlete, [Rider("only", "Мариела Тестова")]);

        var group = Assert.Single(grouping.Groups);
        Assert.Equal(["only"], KeysOf(group));
        Assert.Empty(IdentityMatcher.Group(IdentityKind.Athlete, []).Groups);
    }

    static IdentityObservation Rider(
        string key,
        string? name,
        string? nameEnglish = null,
        string? feiId = null,
        string? legacyId = null
    )
    {
        return new IdentityObservation(key, legacyId, name, nameEnglish, feiId, null, null);
    }

    static string[] KeysOf(IdentityGroup group)
    {
        return [.. group.Members.Select(x => x.Key)];
    }

    static bool Pair(IdentityLink link, string one, string another)
    {
        return (link.One.Key == one && link.Another.Key == another)
            || (link.One.Key == another && link.Another.Key == one);
    }
}
