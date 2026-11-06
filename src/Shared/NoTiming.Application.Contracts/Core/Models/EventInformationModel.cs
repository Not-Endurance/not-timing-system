using MongoDB.Bson.Serialization.Attributes;
using NC.Krud.Abstractions;
using NC.Structures;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects;

namespace NoTiming.Application.Contracts.Core.Models;

public class EventInformationModel : IIdentifiable, ISoftDeletableDocument, IKrudModel<EventInformation>
{
    public static EventInformationModel From(EventInformation eventInformation)
    {
        var model = new EventInformationModel();
        model.MapFrom(eventInformation);
        return model;
    }

    [BsonId]
    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public CountryModel Country { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string? FeiShowId { get; set; }
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public string? TimeZoneId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(EventInformation eventInformation)
    {
        Id = eventInformation.Id;
        Country = CountryModel.From(eventInformation.Country);
        Name = eventInformation.Name;
        Location = eventInformation.Location;
        FeiShowId = eventInformation.FeiShowId;
        StartDay = eventInformation.EventSpan.StartDay;
        EndDay = eventInformation.EventSpan.EndDay;
        TimeZoneId = eventInformation.EventSpan.TimeZoneId;
        IsActive = eventInformation.IsActive;
    }

    public EventInformation MapToEntity()
    {
        var country = Country.MapToEntity();
        var timeZoneId =
            TimeZoneId ?? throw new InvalidOperationException($"Event {Id} has no TimeZoneId. Run the database migrations.");
        var span = new EventSpan(StartDay, EndDay, timeZoneId);
        return new EventInformation(country, Name, Location, span, FeiShowId, Id, IsActive);
    }
}
