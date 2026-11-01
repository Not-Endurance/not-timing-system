using System.Collections;
using System.Globalization;
using System.Resources;
using NTS.Localization.Resources;

namespace NTS.Tests.Unit.NTS.Localization;

/// <summary>
/// The section of the Participations of an Athlete or a Horse is called Participations in each language the app is written in
/// (#639, ADR-0010). A Participation is the act of Participants competing and not a Participant, so the Bulgarian word is
/// "Участия"; "Участници" is the word for the Participants, the Athletes and Horses. No text calls a Participation a
/// Participant, in either language (the owner's answer of 2026-10-23).
/// </summary>
public sealed class ParticipationsTextTests
{
    [Theory]
    [InlineData("en", "Participations")]
    [InlineData("bg", "Участия")]
    public void The_section_is_called_Participations_in_the_language(string language, string expected)
    {
        Assert.Equal(expected, TextOf(language, "Participations_string"));
    }

    [Theory]
    [InlineData("en", "My Participations")]
    [InlineData("bg", "Моите участия")]
    public void The_entry_for_the_Participations_of_the_person_is_called_so_in_the_language(
        string language,
        string expected
    )
    {
        Assert.Equal(expected, TextOf(language, "My_participations_string"));
    }

    [Theory]
    [InlineData("Configure_Participation_string", "Configure Participation", "Конфигурирай Участие")]
    [InlineData(
        "Override_Start_Time_string",
        "Override Start Time for this Participation",
        "Промени времето на Старт за даденото Участие"
    )]
    [InlineData(
        "Select_participations_to_snapshot_string",
        "Select participations to capture time snapshots",
        "Изберете участия за засичане на време"
    )]
    [InlineData("Select_a_participation_string", "Select a Participation", "Изберете участие")]
    [InlineData("Select_participation_to_add_string", "Select a Participation to add", "Изберете участие за добавяне")]
    [InlineData("Participation_not_found_string", "The Participation was not found.", "Участието не е намерено.")]
    [InlineData(
        "Participation_changed_string",
        "The Participation was changed meanwhile and has been read again. What you typed is kept: look at it over what is there now and send it again.",
        "Участието е променено междувременно и е прочетено отново. Въведеното е запазено: прегледайте го върху текущото състояние и го изпратете отново."
    )]
    [InlineData(
        "Timeline_empty_string",
        "No time was recorded for this Participation yet.",
        "Все още няма записано време за това участие."
    )]
    [InlineData(
        "Start_validation_issue_explanation_string",
        "Participation cannot be added in multiple Competitions with different Phases.",
        "Участие не може да бъде добавено в няколко състезания с различни фази."
    )]
    [InlineData(
        "Cannot_add_completed_participations_in_startlist",
        "Cannot add a completed participation in Startlist",
        "Не може да бъде добавено в Стартлист, защото участието е завършило"
    )]
    public void A_text_that_names_a_Participation_calls_it_one_in_each_language(
        string key,
        string english,
        string bulgarian
    )
    {
        Assert.Equal(english, TextOf("en", key));
        Assert.Equal(bulgarian, TextOf("bg", key));
    }

    [Fact]
    public void No_English_text_calls_a_Participation_a_Participant()
    {
        var wrong = TextsOf("en")
            .Where(x => x.Value.Contains("articipant", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .Order()
            .ToArray();

        Assert.True(wrong.Length == 0, $"These texts say Participant: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void A_Bulgarian_text_whose_English_names_a_Participation_does_not_call_it_a_participant()
    {
        var bulgarian = TextsOf("bg");
        var wrong = TextsOf("en")
            .Where(x => x.Value.Contains("articipation", StringComparison.OrdinalIgnoreCase))
            .Where(x =>
                bulgarian.TryGetValue(x.Key, out var text)
                && text.Contains("участник", StringComparison.OrdinalIgnoreCase)
            )
            .Select(x => x.Key)
            .Order()
            .ToArray();

        Assert.True(wrong.Length == 0, $"These texts say участник for a Participation: {string.Join(", ", wrong)}");
    }

    static string? TextOf(string language, string key)
    {
        return ResourcesOf(language).GetString(key);
    }

    static Dictionary<string, string> TextsOf(string language)
    {
        return ResourcesOf(language)
            .Cast<DictionaryEntry>()
            .Where(x => x.Value is string)
            .ToDictionary(x => (string)x.Key, x => (string)x.Value!);
    }

    static ResourceSet ResourcesOf(string language)
    {
        var resources = new ResourceManager(typeof(LocalizedStrings));
        var culture = language == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(language);
        return resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;
    }
}
