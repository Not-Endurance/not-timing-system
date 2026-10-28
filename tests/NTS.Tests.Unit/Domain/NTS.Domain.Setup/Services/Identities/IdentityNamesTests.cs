using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Setup.Services.Identities;

/// <summary>
/// The keys a name is compared by when an Athlete or a Horse is matched (#635, ADR-0010): the name as it is written once it is
/// trimmed, its spaces collapsed and its case folded; the name under the official Bulgarian-to-Latin transliteration, which is
/// what an automatic match may rest on; and a looser Latin key that also folds the spellings people give one name, which only
/// a review may rest on. The names are made up.
/// </summary>
public sealed class IdentityNamesTests
{
    [Theory]
    [InlineData("  Мариела   Тестова ", "МАРИЕЛА ТЕСТОВА")]
    [InlineData("mariela testova", "MARIELA TESTOVA")]
    [InlineData("Mariela\tTestova ", "MARIELA TESTOVA")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void A_name_is_trimmed_with_its_spaces_collapsed_and_its_case_folded(string? name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Normalised(name));
    }

    [Theory]
    [InlineData("Мариела Тестова", "MARIELA TESTOVA")]
    [InlineData("Стоян Тодоров", "STOYAN TODOROV")]
    [InlineData("Йордан Примеров", "YORDAN PRIMEROV")]
    [InlineData("Христо Образцов", "HRISTO OBRAZTSOV")]
    [InlineData("Чавдар Шишманов", "CHAVDAR SHISHMANOV")]
    [InlineData("Жана Щерева", "ZHANA SHTEREVA")]
    [InlineData("Юлиян Проверов", "YULIYAN PROVEROV")]
    [InlineData("Ясен Цонев", "YASEN TSONEV")]
    [InlineData("Ангъл Гълъбов", "ANGAL GALABOV")]
    public void A_Bulgarian_name_is_written_in_Latin_letters_by_the_official_system(string name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Strict(name));
    }

    [Theory]
    [InlineData("Мария", "MARIA")]
    [InlineData("София Тестова", "SOFIA TESTOVA")]
    [InlineData("Мария-Юлия", "MARIA-YULIA")]
    [InlineData("Юлия", "YULIA")]
    public void The_ending_of_a_word_that_is_IYA_is_IA(string name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Strict(name));
    }

    [Theory]
    [InlineData("Юлиян", "YULIYAN")]
    [InlineData("Дияна", "DIYANA")]
    [InlineData("Илияна Тестова", "ILIYANA TESTOVA")]
    public void IYA_inside_a_word_is_IYA(string name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Strict(name));
    }

    [Theory]
    [InlineData("Жельо", "ZHELYO")]
    [InlineData("Дьо Примеров", "DYO PRIMEROV")]
    public void The_soft_sign_before_O_is_Y(string name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Strict(name));
    }

    [Theory]
    [InlineData("АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЬЮЯ")]
    [InlineData("абвгдежзийклмнопрстуфхцчшщъьюя")]
    public void Every_letter_of_the_Bulgarian_alphabet_is_written_by_the_official_system(string alphabet)
    {
        Assert.Equal("ABVGDEZHZIYKLMNOPRSTUFHTSCHSHSHTAYYUYA", IdentityNames.Strict(alphabet));
    }

    [Theory]
    [InlineData("Mariela Testova", "MARIELA TESTOVA")]
    [InlineData("  mariela   testova", "MARIELA TESTOVA")]
    [InlineData("Şahin Örnek", "ŞAHIN ÖRNEK")]
    public void A_name_that_is_already_Latin_is_only_normalised_by_the_strict_system(string name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Strict(name));
    }

    [Theory]
    [InlineData("Ivan 7", "IVAN 7")]
    [InlineData("Ivan-Petar", "IVAN-PETAR")]
    public void Digits_and_punctuation_are_left_as_they_are(string name, string expected)
    {
        Assert.Equal(expected, IdentityNames.Strict(name));
    }

    [Theory]
    [InlineData("Юлия Тестова", "Yulia Testova")]
    [InlineData("Юлия Тестова", "Iuliia Testova")]
    [InlineData("Юлия Тестова", "Julia Testova")]
    [InlineData("Юлия Тестова", "Ulia Testova")]
    [InlineData("Юлия Тестова", "Yuliya Testova")]
    [InlineData("Яна Примерова", "Yana Primerova")]
    [InlineData("Яна Примерова", "Iana Primerova")]
    [InlineData("Яна Примерова", "Jana Primerova")]
    [InlineData("Цветан Образцов", "Tsvetan Obraztsov")]
    [InlineData("Цветан Образцов", "Cvetan Obrazcov")]
    [InlineData("Цветан Образцов", "Tzvetan Obraztsov")]
    [InlineData("Жана Проверова", "Zhana Proverova")]
    [InlineData("Христо Образцов", "Khristo Obraztsov")]
    [InlineData("Щерьо Тестов", "Shcherio Testov")]
    [InlineData("Николай Тестов", "Nikolai Testov")]
    [InlineData("Николай Тестов", "Nikolaj Testov")]
    [InlineData("Александър Примеров", "Alexandar Primerov")]
    [InlineData("Васил Тестов", "Wasil Testov")]
    [InlineData("Şahin Örnek", "Sahin Ornek")]
    public void The_looser_key_is_the_same_for_the_spellings_people_give_one_name(string one, string another)
    {
        Assert.Equal(IdentityNames.Looser(one), IdentityNames.Looser(another));
    }

    [Theory]
    [InlineData("Anna-Maria Testova", "Anna Maria Testova")]
    [InlineData("O'Brien Testov", "OBrien Testov")]
    [InlineData("St. Peter Testov", "St Peter Testov")]
    [InlineData("  -Ivan Testov- ", "Ivan Testov")]
    [InlineData("Ivan_Testov", "Ivan Testov")]
    [InlineData("Anna - Maria Testova", "Anna Maria Testova")]
    [InlineData("Ivan . Testov", "Ivan Testov")]
    [InlineData("Георги Тестов", "Georgiy Testov")]
    public void The_looser_key_ignores_punctuation_and_a_doubled_i(string one, string another)
    {
        Assert.Equal(IdentityNames.Looser(one), IdentityNames.Looser(another));
    }

    [Theory]
    [InlineData("Юлия Тестова")]
    [InlineData("Yyulia Testova")]
    [InlineData("Iiulia Testova")]
    [InlineData("Shchtsvetan Testov")]
    [InlineData("Tstsvetan Testov")]
    [InlineData("Wyuliya Alexandar")]
    [InlineData("Skhch Testov")]
    public void The_key_of_a_key_is_the_key(string name)
    {
        var key = IdentityNames.Looser(name);

        Assert.Equal(key, IdentityNames.Looser(key));
    }

    [Theory]
    [InlineData("Мариела Тестова", "Мариана Тестова")]
    [InlineData("Мариела Тестова", "Мариела Тестов")]
    [InlineData("Иван Примеров", "Петър Примеров")]
    [InlineData("Яна Примерова", "Ана Примерова ")]
    public void The_looser_key_tells_other_names_apart_that_are_not_spellings_of_one(string one, string another)
    {
        Assert.NotEqual(IdentityNames.Looser(one), IdentityNames.Looser(another));
    }

    [Fact]
    public void The_looser_key_of_a_name_that_is_nothing_is_nothing()
    {
        Assert.Equal(string.Empty, IdentityNames.Looser(null));
        Assert.Equal(string.Empty, IdentityNames.Looser("   "));
    }

    [Fact]
    public void The_looser_key_does_not_change_when_it_is_made_again()
    {
        var key = IdentityNames.Looser("Юлия Тестова");

        Assert.Equal(key, IdentityNames.Looser(key));
    }

    [Theory]
    [InlineData("Ivan Testov", "Testov Ivan")]
    [InlineData("  testov   ivan ", "IVAN TESTOV")]
    public void The_words_of_a_name_are_in_one_order_whichever_came_first(string one, string another)
    {
        Assert.Equal(IdentityNames.Sorted(one), IdentityNames.Sorted(another));
    }

    [Fact]
    public void The_sorted_words_are_made_of_the_looser_key()
    {
        Assert.Equal(IdentityNames.Sorted("Testova Julia"), IdentityNames.Sorted("Yulia Testova"));
    }
}
