using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Storage.Documents.Archive.Models;
using NTS.Storage.Documents.Countries;
using NTS.Storage.Documents.Officials;

namespace NTS.Storage.Documents.Archive;

public class ArchiveDocument : Document
{
    public static ArchiveDocument Create(
        EnduranceEvent enduranceEvent,
        IEnumerable<Official> officials,
        IEnumerable<Ranklist> ranklists
    )
    {
        return new ArchiveDocument
        {
            Id = enduranceEvent.Id,
            Country = CountryDocument.Create(enduranceEvent.PopulatedPlace.Country),
            City = enduranceEvent.PopulatedPlace.City,
            Location = enduranceEvent.PopulatedPlace.Location,
            FeiShowId = enduranceEvent.FeiShowId,
            StartDay = enduranceEvent.EventSpan.StartDay,
            EndDay = enduranceEvent.EventSpan.EndDay,
            Officials = officials.Select(OfficialDocument.Create).ToArray(),
            Ranklists = ranklists.Select(RanklistDocumentModel.Create).ToArray(),
        };
    }

    public CountryDocument Country { get; init; } = default!;
    public string City { get; init; } = default!;
    public string? Location { get; init; }
    public string? FeiShowId { get; init; }
    public DateTimeOffset StartDay { get; init; }
    public DateTimeOffset EndDay { get; init; }
    public OfficialDocument[] Officials { get; init; } = default!;
    public RanklistDocumentModel[] Ranklists { get; init; } = default!;

    public ArchiveEntry ToDomain()
    {
        var enduranceEvent = new EnduranceEvent(
            Id,
            Country.ToDomain(),
            City,
            Location ?? "",
            StartDay,
            EndDay,
            FeiShowId
        );
        var officials = Officials.Select(x => x.ToDomain());
        var ranklists = Ranklists.Select(x => x.ToDomain());
        return new ArchiveEntry(enduranceEvent, officials, ranklists);
    }
}
