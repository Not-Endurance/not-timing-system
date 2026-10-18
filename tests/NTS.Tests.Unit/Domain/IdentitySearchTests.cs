using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// Whether a query finds an Athlete or a Horse by a name (#638, ADR-0010): the query and the names are compared by the looser Latin
/// key of the matching service, so a name is found in any spelling and in either script, by a part of it, in any order of its words.
/// </summary>
public sealed class IdentitySearchTests
{
    [Theory]
    [InlineData("Ivan Petrov", "Иван Петров")]
    [InlineData("Иван Петров", "Ivan Petrov")]
    [InlineData("petrov", "Иван Петров")]
    [InlineData("петр", "Ivan Petrov")]
    [InlineData("Yulia Ivanova", "Юлия Иванова")]
    [InlineData("Julia", "Юлия Иванова")]
    [InlineData("Shtilyan", "Щилян Колев")]
    [InlineData("Shchilian", "Щилян Колев")]
    [InlineData("Nikolay", "Николай Тодоров")]
    [InlineData("Nikolai", "Николай Тодоров")]
    public void A_query_finds_a_name_in_the_other_script_and_in_another_spelling(string query, string name)
    {
        Assert.True(IdentitySearch.Matches(query, name));
    }

    [Theory]
    [InlineData("petrov ivan", "Ivan Petrov")]
    [InlineData("iv pe", "Ivan Petrov")]
    [InlineData("VAN PETR", "ivan petrov")]
    [InlineData("  ivan   petrov  ", "Ivan Petrov")]
    [InlineData("o'brien", "OBrien")]
    public void A_query_finds_a_name_by_parts_of_its_words_in_any_order_and_whatever_the_case_and_the_spaces(
        string query,
        string name
    )
    {
        Assert.True(IdentitySearch.Matches(query, name));
    }

    [Fact]
    public void A_query_finds_a_row_by_its_name_or_by_its_name_in_English()
    {
        Assert.True(IdentitySearch.Matches("petrov", "Иван Петров", "Ivan Petrov"));
        Assert.True(IdentitySearch.Matches("Иван Петров", "Тест", "Ivan Petrov"));
        Assert.True(IdentitySearch.Matches("ivan", null, "Ivan Petrov"));
        Assert.True(IdentitySearch.Matches("ivan", "Ivan Petrov", null));
    }

    [Fact]
    public void Every_word_of_the_query_has_to_be_found_in_one_of_the_names()
    {
        Assert.False(IdentitySearch.Matches("ivan georgiev", "Ivan Petrov"));
        Assert.False(IdentitySearch.Matches("ivan georgiev", "Ivan Petrov", "Georgi Ivanov")); // not two names together
    }

    [Theory]
    [InlineData("zhana", "Yana Petrova")] // ж is not folded: Yana and Zhana stay two names
    [InlineData("jana", "Zhana Petrova")]
    [InlineData("petrov", "Todorov")]
    public void A_query_does_not_find_a_name_that_it_is_not_part_of(string query, string name)
    {
        Assert.False(IdentitySearch.Matches(query, name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("'.-")] // nothing is left of it once the marks go
    public void A_query_with_nothing_in_it_finds_nothing(string? query)
    {
        Assert.False(IdentitySearch.Matches(query, "Ivan Petrov", "Иван Петров"));
    }

    [Fact]
    public void A_row_with_no_name_is_not_found()
    {
        Assert.False(IdentitySearch.Matches("ivan", null, " "));
        Assert.False(IdentitySearch.Matches("ivan"));
    }
}
