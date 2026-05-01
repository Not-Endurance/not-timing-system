using MongoDB.Bson;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Setup.Models;
using NTS.Application.Contracts.Shared.Models;
using NTS.Application.Factories;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Tests.Core;

public class EnduranceEventModelMappingTests
{
    [Fact]
    public void ToBsonDocument_WhenSerialized_UsesEventIdAsMongoId()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var setupEvent = new UpcomingEvent(
            "National Championship",
            "Shumen",
            country,
            null,
            null,
            null,
            [CreateCompetition()],
            [],
            [],
            [],
            11
        );
        var model = EnduranceEventModel.From(EnduranceEventFactory.Create(setupEvent));

        var document = model.ToBsonDocument();

        Assert.Equal(11, document["_id"].AsInt32);
        Assert.False(document.Contains("MongoId"));
        Assert.False(document.Contains("Id"));
    }

    [Fact]
    public void Create_WhenSetupEventIsMapped_PreservesNameLocationAndCountry()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var setupEvent = new UpcomingEvent(
            "National Championship",
            "Shumen",
            country,
            null,
            null,
            null,
            [CreateCompetition()],
            [],
            [],
            [],
            11
        );

        var enduranceEvent = EnduranceEventFactory.Create(setupEvent);

        Assert.Equal("National Championship", enduranceEvent.Name);
        Assert.Equal("Shumen", enduranceEvent.Location);
        Assert.Equal(country, enduranceEvent.Country);
    }

    [Fact]
    public void MapToEntity_WhenEnduranceEventModelUsesNameAndLocation_PreservesValues()
    {
        var model = new EnduranceEventModel
        {
            Id = 12,
            Country = CountryModel.From(new Country(1, "Bulgaria", "BG", "BUL", "bg-BG")),
            Name = "Spring Cup",
            Location = "Ring",
            StartDay = DateTimeOffset.UtcNow,
            EndDay = DateTimeOffset.UtcNow.AddDays(1),
        };

        var enduranceEvent = model.MapToEntity();

        Assert.Equal("Spring Cup", enduranceEvent.Name);
        Assert.Equal("Ring", enduranceEvent.Location);
    }

    [Fact]
    public void MapToEntity_WhenUpcomingEventModelUsesLocation_PreservesLocation()
    {
        var model = new UpcomingEventModel
        {
            Id = 14,
            Name = "Event",
            Location = "Kaspichan",
            Country = CountryModel.From(new Country(1, "Bulgaria", "BG", "BUL", "bg-BG")),
            Competitions = [],
            Officials = [],
            Loops = [],
            Combinations = [],
        };

        var upcomingEvent = model.MapToEntity();

        Assert.Equal("Kaspichan", upcomingEvent.Location);
    }

    static NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition CreateCompetition()
    {
        return new(
            "Competition",
            CompetitionType.Qualification,
            CompetitionRuleset.Regional,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            [
                new NTS.Domain.Setup.Aggregates.UpcomingEvents.Phase(
                    new NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop(40, 1),
                    40,
                    null,
                    2
                ),
            ],
            [CreateParticipation()],
            3
        );
    }

    static NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation CreateParticipation()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new NTS.Domain.Setup.Aggregates.Athlete(new Person(["John", "Doe"]), null, country, null, 4);
        var horse = new NTS.Domain.Setup.Aggregates.Horse("Horse", null, 5);
        var combination = new NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination(7, athlete, horse, 6);
        return new NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation(
            false,
            combination,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            id: 8
        );
    }
}
