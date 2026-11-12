using NC.Domain.Exceptions;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Shared.Models;

namespace NoTiming.Application.Tests.Core.Models;

public sealed class EventInformationModelTests
{
    [Fact]
    public void MapToEntity_WhenTimeZoneIdIsMissing_ShouldUseCountryTimeZone()
    {
        var model = CreateModel("BG", timeZoneId: null);

        var eventInformation = model.MapToEntity();

        Assert.Equal("Europe/Sofia", eventInformation.EventSpan.TimeZoneId);
    }

    [Fact]
    public void MapToEntity_WhenTimeZoneIdIsStored_ShouldUseStoredTimeZone()
    {
        var model = CreateModel("BG", timeZoneId: "Europe/Istanbul");

        var eventInformation = model.MapToEntity();

        Assert.Equal("Europe/Istanbul", eventInformation.EventSpan.TimeZoneId);
    }

    [Fact]
    public void MapToEntity_WhenTimeZoneIdIsMissingAndCountryHasNoZone_ShouldThrow()
    {
        var model = CreateModel("XX", timeZoneId: null);

        Assert.Throws<DomainException>(() => model.MapToEntity());
    }

    static EventInformationModel CreateModel(string isoCode, string? timeZoneId)
    {
        return new EventInformationModel
        {
            Id = 1,
            Country = new CountryModel { Id = 1, Name = "Country", IsoCode = isoCode },
            Name = "Event",
            Location = "Location",
            StartDay = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDay = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
            TimeZoneId = timeZoneId,
            IsActive = true,
        };
    }
}
