using NoTiming.Ui.Features.Console.Registry;
using NTS.Contracts.Features.Console.Setup;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// What makes a registry row the same as the one being registered (#646, ADR-0010): the same FEI ID, or the same name once
/// trimmed, collapsed and case-folded, across the name and the name in English. Synthetic names only.
/// </summary>
public sealed class SameRowsTests
{
    static readonly Guid SELF = TestId.Of(1);

    [Fact]
    public void A_name_is_the_same_whatever_its_case_and_spaces_and_whichever_script_it_is_in()
    {
        var rows = new[]
        {
            Row(2, "Иван   Петров", "Ivan Petrov"),
            Row(3, "Мария Тенант", null),
            Row(4, "Georgi Georgiev", null),
        };

        var found = SameRows.Among(rows, null, "  иван петров ", null, null, true);
        var byEnglish = SameRows.Among(rows, null, "Иван Друг", "IVAN  PETROV", null, true);

        Assert.Equal([TestId.Of(2)], found.Select(x => x.Id));
        Assert.Equal([TestId.Of(2)], byEnglish.Select(x => x.Id));
        Assert.All(found.Concat(byEnglish), x => Assert.Equal(SameRowReason.Name, x.Reason));
        Assert.Empty(SameRows.Among(rows, null, "Мария", null, null, true)); // a name that has another in it is not the same
    }

    [Fact]
    public void An_Athletes_FEI_ID_is_a_number_and_a_Horses_is_the_text_it_is()
    {
        var athletes = new[] { Row(2, "Other Name", null, "00012"), Row(3, "Another", null, "120") };
        var horses = new[] { Row(2, "Other Name", null, "abc-12"), Row(3, "Another", null, "ABC-120") };

        var athlete = SameRows.Among(athletes, null, "New Name", null, "12", true);
        var horse = SameRows.Among(horses, null, "New Name", null, "ABC-12", false);
        var notAHorse = SameRows.Among(horses, null, "New Name", null, "12", false);

        Assert.Equal([TestId.Of(2)], athlete.Select(x => x.Id));
        Assert.Equal(SameRowReason.FeiId, Assert.Single(athlete).Reason);
        Assert.Equal([TestId.Of(2)], horse.Select(x => x.Id));
        Assert.Empty(notAHorse);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("0", true)]
    [InlineData("000", true)]
    [InlineData("-----", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("-----", false)]
    public void No_FEI_ID_is_not_a_FEI_ID_that_two_rows_can_share(string? feiId, bool isAthlete)
    {
        var rows = new[] { Row(2, "Other Name", null, feiId) };

        var found = SameRows.Among(rows, null, "New Name", null, feiId, isAthlete);

        Assert.Empty(found);
        Assert.Null(SameRows.FeiIdOf(feiId, isAthlete));
    }

    [Fact]
    public void A_FEI_ID_made_of_one_letter_or_one_digit_is_an_ID_and_a_Horses_digits_are_text_and_spaces_around_it_are_not_part_of_it()
    {
        Assert.Equal("1111", SameRows.FeiIdOf("1111", true));
        Assert.Equal("1111", SameRows.FeiIdOf("1111", false));
        Assert.Equal("AAAA", SameRows.FeiIdOf(" aaaa ", false));
        Assert.Equal("12", SameRows.FeiIdOf(" 0012 ", true));
        Assert.Equal("0012", SameRows.FeiIdOf(" 0012 ", false)); // the digits of a Horse's ID are the text it is: 0012 is not 12
        var horses = new[] { Row(2, "Other Name", null, "0012"), Row(3, "Another", null, "aaaa") };

        Assert.Empty(SameRows.Among(horses, null, "New Name", null, "12", false));
        Assert.Equal([TestId.Of(2)], SameRows.Among(horses, null, "New Name", null, " 0012", false).Select(x => x.Id));
        Assert.Equal([TestId.Of(3)], SameRows.Among(horses, null, "New Name", null, "AAAA ", false).Select(x => x.Id));
    }

    [Fact]
    public void The_row_being_renamed_is_not_the_same_as_itself_and_a_FEI_ID_outweighs_a_name()
    {
        var rows = new[]
        {
            Row(1, "Ivan Petrov", null, "12"),
            Row(2, "Ivan Petrov", null, "99"),
            Row(3, "Else", null, "12"),
        };

        var found = SameRows.Among(rows, SELF, "Ivan Petrov", null, "12", true);

        Assert.Equal([TestId.Of(3), TestId.Of(2)], found.Select(x => x.Id));
        Assert.Equal([SameRowReason.FeiId, SameRowReason.Name], found.Select(x => x.Reason));
    }

    [Fact]
    public void A_name_and_a_FEI_ID_that_are_both_empty_find_nothing_and_the_Tenants_own_rows_come_before_the_others()
    {
        var rows = new[]
        {
            Row(2, "Ivan Petrov", null, null, isOwn: false),
            Row(3, "ivan petrov", null, null, isOwn: true),
        };

        Assert.Empty(SameRows.Among(rows, null, " ", null, null, true));
        Assert.Equal(
            [TestId.Of(3), TestId.Of(2)],
            SameRows.Among(rows, null, "Ivan Petrov", null, null, true).Select(x => x.Id)
        );
    }

    static SameRow Row(int id, string name, string? nameEnglish, string? feiId = null, bool isOwn = true)
    {
        return new SameRow(TestId.Of(id), name, nameEnglish, feiId, isOwn, SameRowReason.Name);
    }
}
