using System.Globalization;
using System.Resources;
using NTS.Localization.Resources;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The section of the Participations of an Athlete or a Horse is called Participations in each language the app is written in
/// (#639, ADR-0010). A Participation is the act of Participants competing and not a Participant, so the Bulgarian word is
/// "Участия"; "Участници" is the word for the Participants, the Athletes and Horses.
/// </summary>
public sealed class ParticipationsTextTests
{
    [Theory]
    [InlineData("en", "Participations")]
    [InlineData("bg", "Участия")]
    [InlineData("tr", "Katılımlar")]
    public void The_section_is_called_Participations_in_the_language(string language, string expected)
    {
        Assert.Equal(expected, TextOf(language, "Participations_string"));
    }

    [Theory]
    [InlineData("en", "My Participations")]
    [InlineData("bg", "Моите участия")]
    [InlineData("tr", "Katılımlarım")]
    public void The_entry_for_the_Participations_of_the_person_is_called_so_in_the_language(
        string language,
        string expected
    )
    {
        Assert.Equal(expected, TextOf(language, "My_participations_string"));
    }

    static string? TextOf(string language, string key)
    {
        var resources = new ResourceManager(typeof(LocalizedStrings));
        var culture = language == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(language);
        return resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!.GetString(key);
    }
}
