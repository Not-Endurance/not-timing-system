using System.Globalization;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Unit.Temporary;

/// <summary>
/// #637, ADR-0009: the Guid a legacy integer becomes is a pure function of its id space, the parent it is scoped to and the integer
/// (a name-based UUID), so that a run that stopped finishes, a rehearsal on a restored copy gives production its Guids, and two
/// machines agree. The literals were made by an independent implementation (RFC 4122 version 5, SHA-1, in Python), not by this one.
/// </summary>
public sealed class LegacyGuidsTests
{
    [Theory]
    [InlineData("Athlete", "7", "cd9b2d55-08a3-5acf-a7a0-b18acab4c929")]
    [InlineData("Athlete", "0", "ed2559d5-dffd-5be6-a9e3-335760072c99")]
    [InlineData("Horse", "7", "e9f78f76-ecb0-55f5-afce-dbd098ff5470")]
    [InlineData("Event", "900", "b81bfb85-72b1-58da-998c-757e3270f4a2")]
    [InlineData("Athlete", "fei:100", "8d4feb4a-089a-55f4-ae9e-5216dd61be19")]
    [InlineData("Athlete", "name:иван петров", "27d6e480-cf45-5227-9dd9-c7aadd4e5533")]
    public void An_unscoped_key_is_the_same_Guid_on_every_machine(string space, string key, string expected)
    {
        Assert.Equal(Guid.Parse(expected), LegacyGuids.Of(space, null, key));
    }

    [Fact]
    public void A_scoped_integer_is_the_Guid_of_its_space_its_parent_and_the_integer()
    {
        var parent = Guid.Parse("b81bfb85-72b1-58da-998c-757e3270f4a2");

        Assert.Equal(Guid.Parse("ef8259d7-2969-56f0-9221-cc62cb86241d"), LegacyGuids.Of("Phase", parent, 12));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("sv-SE")]
    [InlineData("fa-IR")]
    public void The_Guid_of_an_integer_does_not_depend_on_the_culture_of_the_machine(string culture)
    {
        var expected = LegacyGuids.Of("Athlete", null, "-5"); // the minus sign of some cultures is another character
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.Equal(expected, LegacyGuids.Of("Athlete", null, -5));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void An_integer_and_its_text_are_the_same_key()
    {
        Assert.Equal(LegacyGuids.Of("Athlete", null, "7"), LegacyGuids.Of("Athlete", null, 7));
    }

    [Fact]
    public void The_space_the_parent_and_the_integer_each_tell_a_Guid_apart()
    {
        var one = Guid.Parse("b81bfb85-72b1-58da-998c-757e3270f4a2");
        var another = Guid.Parse("ef8259d7-2969-56f0-9221-cc62cb86241d");

        var guids = new[]
        {
            LegacyGuids.Of("Phase", one, 12),
            LegacyGuids.Of("Loop", one, 12),
            LegacyGuids.Of("Phase", another, 12),
            LegacyGuids.Of("Phase", one, 13),
            LegacyGuids.Of("Phase", null, 12),
        };

        Assert.Equal(guids.Length, guids.Distinct().Count());
    }
}
