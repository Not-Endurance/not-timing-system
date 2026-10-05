using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Services.StartValidation;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What the FEI export needs of a Setup (#628): nothing while none of it is configured, and all of it once any of it is.
/// </summary>
public sealed class FeiExportConfigurationTests
{
    [Fact]
    public void A_Setup_with_none_of_the_FEI_values_is_not_asked_for_any()
    {
        var setup = Setup(null, Competition("Ride", FeiValues.None, Rider(1)));

        Assert.Empty(FeiExportConfiguration.MissingOf(setup));
    }

    [Fact]
    public void A_Setup_with_all_of_them_lacks_nothing()
    {
        var setup = Setup("FEI42", Competition("CEI 1*", FeiValues.All, Rider(1, "10012345", "103AB45")));

        Assert.Empty(FeiExportConfiguration.MissingOf(setup));
    }

    [Fact]
    public void A_show_ID_alone_asks_for_nothing_more_while_no_competition_has_a_FEI_value()
    {
        var setup = Setup("FEI42", Competition("Ride", FeiValues.None, Rider(1)));

        Assert.Empty(FeiExportConfiguration.MissingOf(setup));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_competition_with_FEI_values_asks_for_the_show_ID_when_there_is_none(string? showId)
    {
        var setup = Setup(showId, Competition("CEI 1*", FeiValues.All, Rider(1, "10012345", "103AB45")));

        var missing = Assert.Single(FeiExportConfiguration.MissingOf(setup));

        Assert.Equal(new MissingFeiExportValue(FeiExportValue.ShowId, null), missing);
    }

    [Fact]
    public void A_competition_with_one_FEI_value_asks_for_the_other_four_and_for_the_FEI_IDs_of_whoever_rides_in_it()
    {
        var onlyTheCode = FeiValues.None with { EventCode = "CEI1" };
        var setup = Setup("FEI42", Competition("CEI 1*", onlyTheCode, Rider(1)));

        var missing = FeiExportConfiguration.MissingOf(setup);

        Assert.Equal(
            [
                new MissingFeiExportValue(FeiExportValue.EventId, "CEI 1*"),
                new MissingFeiExportValue(FeiExportValue.CompetitionId, "CEI 1*"),
                new MissingFeiExportValue(FeiExportValue.Rule, "CEI 1*"),
                new MissingFeiExportValue(FeiExportValue.ScheduleNumber, "CEI 1*"),
                new MissingFeiExportValue(FeiExportValue.FeiId, "#1, Horse 1"),
                new MissingFeiExportValue(FeiExportValue.FeiId, "#1, Athlete 1"),
            ],
            missing
        );
    }

    [Theory]
    [InlineData(FeiExportValue.EventId)]
    [InlineData(FeiExportValue.EventCode)]
    [InlineData(FeiExportValue.CompetitionId)]
    [InlineData(FeiExportValue.Rule)]
    [InlineData(FeiExportValue.ScheduleNumber)]
    public void Any_one_FEI_value_of_a_competition_makes_it_ask_for_the_other_four(FeiExportValue only)
    {
        var setup = Setup(
            "FEI42",
            Competition("CEI 1*", FeiValues.None.With(only, "x"), Rider(1, "10012345", "103AB45"))
        );

        var missing = FeiExportConfiguration.MissingOf(setup);

        var others = new[]
        {
            FeiExportValue.EventId,
            FeiExportValue.EventCode,
            FeiExportValue.CompetitionId,
            FeiExportValue.Rule,
            FeiExportValue.ScheduleNumber,
        }.Where(x => x != only);
        Assert.Equal(others.Select(x => new MissingFeiExportValue(x, "CEI 1*")), missing);
    }

    [Theory]
    [InlineData(FeiExportValue.EventId, null)]
    [InlineData(FeiExportValue.EventId, " ")]
    [InlineData(FeiExportValue.EventCode, null)]
    [InlineData(FeiExportValue.EventCode, " ")]
    [InlineData(FeiExportValue.CompetitionId, null)]
    [InlineData(FeiExportValue.CompetitionId, " ")]
    [InlineData(FeiExportValue.Rule, null)]
    [InlineData(FeiExportValue.Rule, " ")]
    [InlineData(FeiExportValue.ScheduleNumber, null)]
    [InlineData(FeiExportValue.ScheduleNumber, " ")]
    public void Each_value_of_a_competition_that_is_blank_is_missing_and_only_that_one(
        FeiExportValue value,
        string? blank
    )
    {
        var setup = Setup(
            "FEI42",
            Competition("CEI 1*", FeiValues.All.With(value, blank), Rider(1, "10012345", "103AB45"))
        );

        var missing = Assert.Single(FeiExportConfiguration.MissingOf(setup));

        Assert.Equal(new MissingFeiExportValue(value, "CEI 1*"), missing);
    }

    [Fact]
    public void A_Horse_and_an_Athlete_without_a_FEI_ID_are_missing_each_as_the_number_and_the_name_they_ride_as()
    {
        var setup = Setup(
            "FEI42",
            Competition("CEI 1*", FeiValues.All, Rider(7, null, "103AB45"), Rider(8, "10012345", " "))
        );

        var missing = FeiExportConfiguration.MissingOf(setup);

        Assert.Equal(
            [
                new MissingFeiExportValue(FeiExportValue.FeiId, "#7, Athlete 7"),
                new MissingFeiExportValue(FeiExportValue.FeiId, "#8, Horse 8"),
            ],
            missing
        );
    }

    [Fact]
    public void A_competition_with_none_of_the_FEI_values_is_not_asked_for_them_beside_one_that_has_some()
    {
        var setup = Setup(
            "FEI42",
            Competition("CEI 1*", FeiValues.All, Rider(1, "10012345", "103AB45")),
            Competition("Local", FeiValues.None, Rider(2))
        );

        Assert.Empty(FeiExportConfiguration.MissingOf(setup));
    }

    static SetupConfigureEvent Setup(string? showId, params SetupCompetition[] competitions)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        return new SetupConfigureEvent("Ride", "Sofia", country, showId, competitions, [], [], [], TestId.Of(2));
    }

    static SetupCompetition Competition(string name, FeiValues fei, params SetupParticipation[] riders)
    {
        return new SetupCompetition(
            name,
            CompetitionRuleset.FEI,
            new DateTimeOffset(2030, 5, 21, 8, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            fei.EventId,
            fei.EventCode,
            fei.CompetitionId,
            fei.Rule,
            fei.ScheduleNumber,
            [],
            riders,
            TestId.Of(name.Length + 100)
        );
    }

    static SetupParticipation Rider(int number, string? athleteFeiId = null, string? horseFeiId = null)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new SetupAthlete($"Athlete {number}", null, athleteFeiId, country, null, TestId.Of(number));
        var horse = new SetupHorse($"Horse {number}", null, horseFeiId, TestId.Of(number + 1_000));
        var combination = new SetupCombination(number, athlete, horse, TestId.Of(number + 2_000));
        return new SetupParticipation(
            false,
            combination,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            TestId.Of(number + 3_000)
        );
    }

    sealed record FeiValues
    {
        public static FeiValues All { get; } =
            new()
            {
                EventId = "FEI-EVENT",
                EventCode = "CEI1",
                CompetitionId = "FEI-COMPETITION",
                Rule = "Rule",
                ScheduleNumber = "1",
            };

        public static FeiValues None { get; } = new();

        public string? EventId { get; init; }
        public string? EventCode { get; init; }
        public string? CompetitionId { get; init; }
        public string? Rule { get; init; }
        public string? ScheduleNumber { get; init; }

        public FeiValues With(FeiExportValue value, string? text)
        {
            return value switch
            {
                FeiExportValue.EventId => this with { EventId = text },
                FeiExportValue.EventCode => this with { EventCode = text },
                FeiExportValue.CompetitionId => this with { CompetitionId = text },
                FeiExportValue.Rule => this with { Rule = text },
                FeiExportValue.ScheduleNumber => this with { ScheduleNumber = text },
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
            };
        }
    }
}
