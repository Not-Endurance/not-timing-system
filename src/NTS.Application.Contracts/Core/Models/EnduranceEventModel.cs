using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;


namespace NTS.Application.Contracts.Core.Models;

public class EnduranceEventModel
    : IIdentifiable,
        IMongoIdentityDocument,
        ISoftDeletableDocument,
        IKrudModel<EnduranceEvent>
{
    public static EnduranceEventModel From(EnduranceEvent enduranceEvent)
    {
        var model = new EnduranceEventModel();
        model.MapFrom(enduranceEvent);
        return model;
    }

    [JsonIgnore]
    [BsonId]
    public string MongoId { get; set; } = "";
    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public CountryModel Country { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string? FeiShowId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(EnduranceEvent enduranceEvent)
    {
        Id = enduranceEvent.Id;
        Country = CountryModel.From(enduranceEvent.Country);
        Name = enduranceEvent.Name;
        Location = enduranceEvent.Location;
        FeiShowId = enduranceEvent.FeiShowId;
        FeiId = enduranceEvent.FeiId;
        FeiEventCode = enduranceEvent.FeiEventCode;
        StartDay = enduranceEvent.EventSpan.StartDay;
        EndDay = enduranceEvent.EventSpan.EndDay;
    }

    public EnduranceEvent MapToEntity()
    {
        var country = Country.MapToEntity();
        var span = new EventSpan(StartDay, EndDay);
        return new EnduranceEvent(country, Name, Location, span, FeiShowId, FeiId, FeiEventCode, Id);
    }
}


