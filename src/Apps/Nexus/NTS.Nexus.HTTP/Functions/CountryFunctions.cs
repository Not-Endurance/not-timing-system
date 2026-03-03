using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Async.Extensions;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class CountryFunctions : FunctionBase
{
    readonly IRepository<CountryModel> _countries;

    public CountryFunctions(
        IFunctionLogger<CountryFunctions> logger,
        IRepository<CountryModel> countries,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _countries = countries;
    }

    [Function("countries-insert")]
    public Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "countries")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Insert), async () =>
        {
            var country = await ReadBody<Country>(request);
            if (country == null)
            {
                return UnexpectedPayload<Country>();
            }

            var document = CountryModel.MapFrom(country);
            await ExecuteWithTelemetry("RepositoryCreate", () => _countries.Create(document));
            return new OkObjectResult($"Inserted {country}");
        });
    }

    [Function("countries-update")]
    public Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "countries")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Update), async () =>
        {
            var country = await ReadBody<Country>(request);
            if (country == null)
            {
                return UnexpectedPayload<Country>();
            }

            var document = CountryModel.MapFrom(country);
            await ExecuteWithTelemetry("RepositoryUpdate", () => _countries.Update(document));
            return new OkObjectResult($"Updated {country}");
        });
    }

    [Function("countries-list")]
    public Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "countries")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(List), async () =>
        {
            var countries = await ExecuteWithTelemetry("RepositoryReadMany", () => _countries.ReadMany().Select(x => x.MapToDomain()));
            return new OkObjectResult(countries);
        });
    }
}
