using MongoDB.Bson;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Setup.Models;
using NTS.Application.Contracts.Shared.Models;
using NTS.Application.Factories;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Tests.Core;

public class EventInformationModelMappingTests
{
    [Fact]
    public void ToBsonDocument_WhenSerialized_UsesEventIdAsMongoId()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var setupEvent = new ConfigureEvent(
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
        var model = EventInformationModel.From(EventInformationFactory.Create(setupEvent));

        var document = model.ToBsonDocument();

        Assert.Equal(11, document["_id"].AsInt32);
        Assert.True(document["IsActive"].AsBoolean);
        Assert.False(document.Contains("MongoId"));
        Assert.False(document.Contains("Id"));
    }

    [Fact]
    public void Create_WhenSetupEventIsMapped_PreservesNameLocationAndCountry()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var setupEvent = new ConfigureEvent(
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

        var eventInformation = EventInformationFactory.Create(setupEvent);

        Assert.Equal("National Championship", eventInformation.Name);
        Assert.Equal("Shumen", eventInformation.Location);
        Assert.Equal(country, eventInformation.Country);
        Assert.True(eventInformation.IsActive);
    }

    [Fact]
    public void MapToEntity_WhenEventInformationModelUsesNameAndLocation_PreservesValues()
    {
        var model = new EventInformationModel
        {
            Id = 12,
            Country = CountryModel.From(new Country(1, "Bulgaria", "BG", "BUL", "bg-BG")),
            Name = "Spring Cup",
            Location = "Ring",
            StartDay = DateTimeOffset.UtcNow,
            EndDay = DateTimeOffset.UtcNow.AddDays(1),
            IsActive = true,
        };

        var eventInformation = model.MapToEntity();

        Assert.Equal("Spring Cup", eventInformation.Name);
        Assert.Equal("Ring", eventInformation.Location);
        Assert.True(eventInformation.IsActive);
    }

    [Fact]
    public void MapRoundTrip_WhenEventInformationIsInactive_PreservesIsActive()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var eventInformation = new EventInformation(
            country,
            "Autumn Cup",
            "Field",
            new EventSpan(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1)),
            null,
            null,
            null,
            18,
            isActive: false
        );

        var model = EventInformationModel.From(eventInformation);
        var mapped = model.MapToEntity();

        Assert.False(model.IsActive);
        Assert.False(mapped.IsActive);
    }

    [Fact]
    public void MapToEntity_WhenConfigureEventModelUsesLocation_PreservesLocation()
    {
        var model = new ConfigureEventModel
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

        var configureEvent = model.MapToEntity();

        Assert.Equal("Kaspichan", configureEvent.Location);
    }

    static NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition CreateCompetition()
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
                new NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase(
                    new NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop(40, 1),
                    40,
                    null,
                    2
                ),
            ],
            [CreateParticipation()],
            3
        );
    }

    static NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation CreateParticipation()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new NTS.Domain.Setup.Aggregates.Athlete(new Person(["John", "Doe"]), null, country, null, 4);
        var horse = new NTS.Domain.Setup.Aggregates.Horse("Horse", null, 5);
        var combination = new NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination(7, athlete, horse, 6);
        return new NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation(
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
