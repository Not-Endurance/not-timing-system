using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>The countries as the Api serves them (#603): JSON:API documents at <c>/api/countries</c> (ADR-0008).</summary>
public class CountryApiRepository : JsonApiRepository<Country, CountryModel>
{
    public CountryApiRepository(JsonApiClient client)
        : base("countries", client) { }
}
