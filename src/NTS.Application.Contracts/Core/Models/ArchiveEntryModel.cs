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

public class ArchiveEntryModel : IDocument, IKrudModel<ArchiveEntry>
{
    public static ArchiveEntryModel From(
        EnduranceEvent enduranceEvent,
        IEnumerable<Official> officials,
        IEnumerable<Ranklist> ranklists
    )
    {
        return new ArchiveEntryModel
        {
            Id = enduranceEvent.Id,
            Country = CountryModel.From(enduranceEvent.Country),
            Name = enduranceEvent.Name,
            Location = enduranceEvent.Location,
            FeiShowId = enduranceEvent.FeiShowId,
            FeiId = enduranceEvent.FeiId,
            FeiEventCode = enduranceEvent.FeiEventCode,
            StartDay = enduranceEvent.EventSpan.StartDay,
            EndDay = enduranceEvent.EventSpan.EndDay,
            Officials = officials.Select(OfficialModel.MapFrom).ToArray(),
            Ranklists = ranklists.Select(RanklistModel.MapFrom).ToArray(),
        };
    }

    public int Id { get; set; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public CountryModel Country { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string? FeiShowId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public OfficialModel[] Officials { get; set; } = default!;
    public RanklistModel[] Ranklists { get; set; } = default!;

    public void MapFrom(ArchiveEntry archiveEntry)
    {
        Id = archiveEntry.EnduranceEvent.Id;
        Country = CountryModel.From(archiveEntry.EnduranceEvent.Country);
        Name = archiveEntry.EnduranceEvent.Name;
        Location = archiveEntry.EnduranceEvent.Location;
        FeiShowId = archiveEntry.EnduranceEvent.FeiShowId;
        FeiId = archiveEntry.EnduranceEvent.FeiId;
        FeiEventCode = archiveEntry.EnduranceEvent.FeiEventCode;
        StartDay = archiveEntry.EnduranceEvent.EventSpan.StartDay;
        EndDay = archiveEntry.EnduranceEvent.EventSpan.EndDay;
        Officials = archiveEntry.Officials.Select(OfficialModel.MapFrom).ToArray();
        Ranklists = archiveEntry.Ranklists.Select(RanklistModel.MapFrom).ToArray();
    }

    public ArchiveEntry MapToEntity()
    {
        var country = Country.MapToEntity();
        var span = new EventSpan(StartDay, EndDay);
        var enduranceEvent = new EnduranceEvent(country, Name, Location, span, FeiShowId, FeiId, FeiEventCode, Id);
        var officials = Officials.Select(x => x.MapToEntity());
        var ranklists = Ranklists.Select(x => x.MapToEntity());
        return new ArchiveEntry(enduranceEvent, officials, ranklists);
    }
}
