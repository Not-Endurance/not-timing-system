using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Setup.Services.Identities;

/// <summary>
/// What makes two copies of an Athlete or a Horse the same one, and which rule decided it (#635, ADR-0010): the same FEI ID, the
/// same name once normalised, the same name by the official transliteration, the same legacy id other than 0 are a match; a
/// spelling variant, a near-miss and a conflict are for a person to review. The country and the club are evidence and decide
/// nothing. Every name here is made up.
/// </summary>
public sealed class IdentityMatchingTests
{
    [Fact]
    public void The_same_FEI_ID_is_a_match_whatever_the_zeros_it_was_written_with_and_even_when_the_names_are_spelled_differently()
    {
        var one = Rider("Юлиян Проверов", feiId: "10012345");
        var another = Rider("Iulian Proverov", feiId: "0010012345");

        Assert.Equal(MatchClass.SameFeiId, Between(one, another));
    }

    [Fact]
    public void The_FEI_ID_decides_before_the_name_does()
    {
        var one = Rider("Мариела Тестова", feiId: "10012345");
        var another = Rider("Мариела Тестова", feiId: "10012345");

        Assert.Equal(MatchClass.SameFeiId, Between(one, another));
    }

    [Fact]
    public void A_Horses_FEI_ID_is_matched_as_text_in_any_case()
    {
        var one = Rider("Тест Огън", feiId: "103bk17");
        var another = Rider("Test Ogan", feiId: " 103BK17 ");

        Assert.Equal(MatchClass.SameFeiId, Between(one, another, IdentityKind.Horse));
    }

    [Fact]
    public void The_same_FEI_ID_under_names_that_share_nothing_is_a_conflict_and_not_a_match()
    {
        var one = Rider("Мариела Тестова", feiId: "10012345");
        var another = Rider("Борис Примеров", feiId: "10012345");

        Assert.Equal(MatchClass.Conflict, Between(one, another));
    }

    [Fact]
    public void The_same_FEI_ID_under_names_that_are_a_near_miss_is_a_match()
    {
        var one = Rider("Ivan Testov", feiId: "10012345");
        var another = Rider("Ivan Testoff", feiId: "10012345");

        Assert.Equal(MatchClass.SameFeiId, Between(one, another));
    }

    [Fact]
    public void An_exact_name_is_a_match_once_it_is_trimmed_collapsed_and_case_folded()
    {
        var one = Rider("  Мариела   ТЕСТОВА ");
        var another = Rider("мариела тестова");

        Assert.Equal(MatchClass.ExactName, Between(one, another));
    }

    [Fact]
    public void Names_are_compared_across_the_name_and_the_name_in_English_of_both_copies()
    {
        var one = Rider("Мариела Тестова", "Mariela Testova");
        var another = Rider("MARIELA TESTOVA");

        Assert.Equal(MatchClass.ExactName, Between(one, another));
    }

    [Fact]
    public void A_name_in_both_scripts_is_the_same_name_by_the_official_transliteration()
    {
        var cyrillic = Rider("Мариела Тестова");
        var latin = Rider("Mariela Testova");

        Assert.Equal(MatchClass.StrictTransliteration, Between(cyrillic, latin));
    }

    [Fact]
    public void A_name_that_is_in_Latin_in_the_name_in_English_is_matched_by_the_transliteration_of_the_other_copys_name()
    {
        var cyrillic = Rider("Юлиян Проверов", "Iuliyan Proverov");
        var latin = Rider("Yuliyan Proverov");

        Assert.Equal(MatchClass.StrictTransliteration, Between(cyrillic, latin));
    }

    [Fact]
    public void A_spelling_that_only_the_looser_key_makes_the_same_is_for_a_review_and_not_a_match()
    {
        var official = Rider("Юлия Тестова");
        var variant = Rider("Iuliia Testova");

        Assert.Equal(MatchClass.SpellingVariant, Between(official, variant));
    }

    [Fact]
    public void Strict_and_looser_transliteration_are_told_apart_by_their_classes()
    {
        var cyrillic = Rider("Яна Примерова");

        Assert.Equal(MatchClass.StrictTransliteration, Between(cyrillic, Rider("Yana Primerova")));
        Assert.Equal(MatchClass.SpellingVariant, Between(cyrillic, Rider("Iana Primerova")));
        Assert.Equal(MatchClass.SpellingVariant, Between(cyrillic, Rider("Jana Primerova")));
    }

    [Theory]
    [InlineData("Ivan Testov", "Ivan Testoff")]
    [InlineData("Ivan Testov", "Ivan Testova")]
    [InlineData("Ivan Testov", "Ivan Tesstov")]
    [InlineData("Ivan Testov", "Ivana Testova")]
    public void A_name_that_is_one_or_two_edits_away_is_a_near_miss(string one, string another)
    {
        Assert.Equal(MatchClass.NearMiss, Between(Rider(one), Rider(another)));
    }

    [Fact]
    public void The_same_words_in_another_order_are_a_near_miss()
    {
        Assert.Equal(MatchClass.NearMiss, Between(Rider("Ivan Testov"), Rider("Testov Ivan")));
    }

    [Fact]
    public void A_name_three_edits_away_is_not_a_near_miss()
    {
        Assert.Null(Between(Rider("Ivan Testov"), Rider("Ivan Testovich")));
    }

    [Fact]
    public void A_short_name_needs_to_be_closer_than_a_long_one_to_be_a_near_miss()
    {
        Assert.Equal(MatchClass.NearMiss, Between(Rider("Ana"), Rider("Anu"), IdentityKind.Horse));
        Assert.Null(Between(Rider("Max"), Rider("Mia"), IdentityKind.Horse));
        Assert.Null(Between(Rider("Ali"), Rider("Alba"), IdentityKind.Horse));
        Assert.Equal(MatchClass.NearMiss, Between(Rider("Fiorella"), Rider("Fiorela"), IdentityKind.Horse));
        Assert.Null(Between(Rider("Anna"), Rider("Anbe"), IdentityKind.Horse)); // two edits of four letters is half, and half is not fewer
        Assert.Equal(MatchClass.NearMiss, Between(Rider("Boris"), Rider("Bonas"), IdentityKind.Horse)); // two edits of five are fewer
    }

    [Fact]
    public void Names_that_are_nothing_alike_are_no_match()
    {
        Assert.Null(Between(Rider("Мариела Тестова"), Rider("Борис Примеров")));
    }

    [Fact]
    public void Copies_that_have_no_name_match_nothing()
    {
        Assert.Null(Between(Rider(null), Rider("  ")));
    }

    [Fact]
    public void The_same_legacy_id_is_a_match_when_the_names_share_something()
    {
        var one = Rider("Ivan Testov", legacyId: "17");
        var another = Rider("Ivan Testoff", legacyId: "17");

        Assert.Equal(MatchClass.SameLegacyId, Between(one, another));
    }

    [Fact]
    public void The_same_legacy_id_under_names_that_share_nothing_is_a_conflict()
    {
        var one = Rider("Мариела Тестова", legacyId: "17");
        var another = Rider("Борис Примеров", legacyId: "17");

        Assert.Equal(MatchClass.Conflict, Between(one, another));
    }

    [Fact]
    public void A_name_decides_before_a_legacy_id_does()
    {
        var one = Rider("Мариела Тестова", legacyId: "17");
        var another = Rider("мариела тестова", legacyId: "17");

        Assert.Equal(MatchClass.ExactName, Between(one, another));
    }

    [Fact]
    public void The_legacy_id_0_is_no_id_and_matches_nothing()
    {
        var one = Rider("Мариела Тестова", legacyId: "0");
        var another = Rider("Борис Примеров", legacyId: "0");

        Assert.Null(Between(one, another));
        Assert.Null(Between(Rider("Мариела Тестова", legacyId: "0"), Rider("Борис Примеров")));
        Assert.Equal(
            MatchClass.ExactName,
            Between(Rider("Мариела Тестова", legacyId: "0"), Rider("МАРИЕЛА ТЕСТОВА", legacyId: "0"))
        );
    }

    [Fact]
    public void Equal_names_under_FEI_IDs_that_are_one_digit_apart_are_a_conflict()
    {
        var one = Rider("Мариела Тестова", feiId: "10012345");
        var another = Rider("Мариела Тестова", feiId: "10012346");

        Assert.Equal(MatchClass.Conflict, Between(one, another));
    }

    [Fact]
    public void The_same_name_in_another_script_or_spelled_another_way_under_different_FEI_IDs_is_a_conflict_too()
    {
        Assert.Equal(
            MatchClass.Conflict,
            Between(Rider("Мариела Тестова", feiId: "10012345"), Rider("Mariela Testova", feiId: "10012346"))
        );
        Assert.Equal(
            MatchClass.Conflict,
            Between(Rider("Юлия Тестова", feiId: "10012345"), Rider("Iuliia Testova", feiId: "10012346"))
        );
    }

    [Fact]
    public void Names_that_are_only_a_near_miss_under_different_FEI_IDs_are_two_people()
    {
        var one = Rider("Ivan Testov", feiId: "10012345");
        var another = Rider("Ivan Testova", feiId: "10012346");

        Assert.Null(Between(one, another));
    }

    [Fact]
    public void A_legacy_id_that_is_shared_under_different_FEI_IDs_is_a_conflict()
    {
        var one = Rider("Ivan Testov", feiId: "10012345", legacyId: "17");
        var another = Rider("Ivan Testoff", feiId: "10012346", legacyId: "17");

        Assert.Equal(MatchClass.Conflict, Between(one, another));
    }

    [Fact]
    public void A_FEI_ID_that_is_missing_or_a_placeholder_does_not_hold_a_match_back()
    {
        Assert.Equal(
            MatchClass.ExactName,
            Between(Rider("Мариела Тестова", feiId: "10012345"), Rider("Мариела Тестова", feiId: "-----"))
        );
        Assert.Equal(
            MatchClass.ExactName,
            Between(Rider("Мариела Тестова", feiId: "10012345"), Rider("Мариела Тестова", feiId: "0"))
        );
    }

    [Fact]
    public void The_country_and_the_club_are_evidence_and_decide_nothing()
    {
        var bulgarian = Rider("Мариела Тестова", country: "BGR", club: "Клуб Пример");
        var again = Rider("Мариела Тестова", country: "BUL", club: "Another Club");

        Assert.Equal(MatchClass.ExactName, Between(bulgarian, again));
        Assert.Null(Between(Rider("Мариела Тестова", country: "BGR"), Rider("Борис Примеров", country: "BGR")));
        Assert.Equal(
            MatchClass.Conflict,
            Between(
                Rider("Мариела Тестова", feiId: "10012345", country: "BGR"),
                Rider("Мариела Тестова", feiId: "10012346", country: "BGR")
            )
        );
    }

    [Fact]
    public void A_Horse_with_the_same_name_as_another_is_the_same_Horse_when_nothing_says_otherwise()
    {
        var one = Rider("Тест Огън");
        var another = Rider("  тест   огън ");

        Assert.Equal(MatchClass.ExactName, Between(one, another, IdentityKind.Horse));
    }

    [Fact]
    public void A_Horse_with_the_same_name_as_another_under_different_FEI_IDs_is_a_conflict()
    {
        var one = Rider("Тест Огън", feiId: "103BK17");
        var another = Rider("Тест Огън", feiId: "103BK18");

        Assert.Equal(MatchClass.Conflict, Between(one, another, IdentityKind.Horse));
    }

    [Theory]
    [InlineData(MatchClass.SameFeiId, true)]
    [InlineData(MatchClass.ExactName, true)]
    [InlineData(MatchClass.StrictTransliteration, true)]
    [InlineData(MatchClass.SameLegacyId, true)]
    [InlineData(MatchClass.SpellingVariant, false)]
    [InlineData(MatchClass.NearMiss, false)]
    [InlineData(MatchClass.Conflict, false)]
    public void Four_classes_are_automatic_and_the_rest_are_for_a_review(MatchClass match, bool isAutomatic)
    {
        Assert.Equal(isAutomatic, match.IsAutomatic());
    }

    [Fact]
    public void The_copies_a_row_is_alike_are_found_among_many_each_with_the_rule_that_decided_it_and_in_the_order_they_came()
    {
        var typed = Rider("Мариела Тестова", feiId: "10012345");
        var sameFei = Rider("Друго Име", feiId: "0010012345"); // the FEI ID is all that the names have in common: a conflict
        var sameName = Rider("мариела  тестова");
        var latin = Rider("Mariela Testova");
        var variant = Rider("Mariyela Testova");
        var nothing = Rider("Борис Примеров");

        var found = IdentityMatcher.Among(IdentityKind.Athlete, typed, [nothing, latin, sameFei, variant, sameName]);

        Assert.Equal(
            [
                (latin.Key, MatchClass.StrictTransliteration),
                (sameFei.Key, MatchClass.Conflict),
                (variant.Key, MatchClass.SpellingVariant),
                (sameName.Key, MatchClass.ExactName),
            ],
            found.Select(x => (x.Another.Key, x.Class))
        );
        Assert.All(found, x => Assert.Same(typed, x.One));
    }

    [Fact]
    public void A_row_is_not_alike_a_copy_of_itself_in_the_list_it_is_asked_among()
    {
        var typed = Rider("Мариела Тестова");

        Assert.Empty(IdentityMatcher.Among(IdentityKind.Athlete, typed, [typed]));
    }

    static IdentityObservation Rider(
        string? name,
        string? nameEnglish = null,
        string? feiId = null,
        string? legacyId = null,
        string? country = null,
        string? club = null
    )
    {
        return new IdentityObservation(Guid.NewGuid().ToString(), legacyId, name, nameEnglish, feiId, country, club);
    }

    static MatchClass? Between(
        IdentityObservation one,
        IdentityObservation another,
        IdentityKind kind = IdentityKind.Athlete
    )
    {
        var verdict = IdentityMatcher.Classify(kind, one, another);
        Assert.Equal(verdict, IdentityMatcher.Classify(kind, another, one)); // it does not matter which is asked first
        return verdict;
    }
}
