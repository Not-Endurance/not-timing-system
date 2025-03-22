using Not.Domain;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Storage.Documents.Countries;
using NTS.Storage.Documents.Archive.Models;
using NTS.Storage.Documents.Officials;

namespace NTS.Storage.Documents.Archive;

public class ArchiveDocument : Document
{
    public ArchiveDocument(EnduranceEvent enduranceEvent, IEnumerable<Official> officials, IEnumerable<Ranklist> ranklists)
        : base(enduranceEvent.Id)
    {
        Country = new CountryDocument(enduranceEvent.PopulatedPlace.Country);
        City = enduranceEvent.PopulatedPlace.City;
        Location = enduranceEvent.PopulatedPlace.Location;
        StartDay = enduranceEvent.EventSpan.StartDay;
        EndDay = enduranceEvent.EventSpan.EndDay;
        Officials = officials.Select(x => new OfficialDocument(x)).ToArray();
        Ranklists = ranklists.Select(x => new RanklistDocumentModel(x)).ToArray();
    }

    public CountryDocument Country { get; init; }
    public string City { get; init; }
    public string? Location { get; init; }
    public DateTimeOffset StartDay { get; init; }
    public DateTimeOffset EndDay { get; init; }
    public OfficialDocument[] Officials { get; init; }
    public RanklistDocumentModel[] Ranklists { get; init; }
}
