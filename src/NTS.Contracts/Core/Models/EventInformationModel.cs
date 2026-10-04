using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Contracts.Core.Models;

public class EventInformationModel : IIdentifiable, ISoftDeletableDocument, IKrudModel<EventInformation>
{
    public static EventInformationModel From(EventInformation eventInformation)
    {
        var model = new EventInformationModel();
        model.MapFrom(eventInformation);
        return model;
    }

    [BsonId]
    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public Guid? MainOperatorId { get; set; }
    public CountryModel Country { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string? FeiShowId { get; set; }
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public bool IsActive { get; set; }
    public RegionalRulesModel? RegionalRules { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(EventInformation eventInformation)
    {
        Id = eventInformation.Id;
        TenantId = eventInformation.TenantId;
        MainOperatorId = eventInformation.MainOperatorId;
        Country = CountryModel.From(eventInformation.Country);
        Name = eventInformation.Name;
        Location = eventInformation.Location;
        FeiShowId = eventInformation.FeiShowId;
        StartDay = eventInformation.EventSpan.StartDay;
        EndDay = eventInformation.EventSpan.EndDay;
        IsActive = eventInformation.IsActive;
        RegionalRules = RegionalRulesModel.From(eventInformation.RegionalRules);
    }

    public EventInformation MapToEntity()
    {
        var country = Country.MapToEntity();
        var span = new EventSpan(StartDay, EndDay);
        return new EventInformation(
            country,
            Name,
            Location,
            span,
            FeiShowId,
            Id,
            IsActive,
            RegionalRules?.MapToEntity(),
            TenantId,
            MainOperatorId
        );
    }
}
