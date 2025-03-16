using Not.Domain;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Storage.Documents.Countries;
using NTS.Storage.Documents.EnduranceEvents.Models;
using NTS.Storage.Documents.Officials;

namespace NTS.Storage.Documents.EnduranceEvents;

public class EnduranceEventDocument : Document
{
    public EnduranceEventDocument(
        EnduranceEvent enduranceEvent,
        IEnumerable<Official> officials,
        IEnumerable<Ranklist> ranklists
    )
        : base(enduranceEvent.Id)
    {
        Country = new CountryDocument(enduranceEvent.PopulatedPlace.Country);
        City = enduranceEvent.PopulatedPlace.City;
        Location = enduranceEvent.PopulatedPlace.Location;
        StartDay = enduranceEvent.EventSpan.StartDay;
        EndDay = enduranceEvent.EventSpan.EndDay;
        Officials = officials.Select(x => new OfficialDocument(x)).ToArray();
        Ranklists = ranklists.Select(x => new RanklistModel(x)).ToArray();
    }

    public CountryDocument Country { get; init; }
    public string City { get; init; }
    public string? Location { get; init; }
    public DateTimeOffset StartDay { get; init; }
    public DateTimeOffset EndDay { get; init; }
    public OfficialDocument[] Officials { get; init; }
    public RanklistModel[] Ranklists { get; init; }

    public EnduranceEvent ToDomain()
    {
        return new EnduranceEvent(Id, Country.ToDomain(), City, Location ?? "", StartDay, EndDay, null, null, null); // TODO: fix for FEI
    }
}
