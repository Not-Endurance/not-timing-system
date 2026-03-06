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
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "countries")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var country = await ReadBody<Country>(request);
        if (country == null)
        {
            return UnexpectedPayload<Country>();
        }

        var document = CountryModel.MapFrom(country);
        await _countries.Create(document);
        return new OkObjectResult($"Inserted {country}");
    }

    [Function("countries-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "countries")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var country = await ReadBody<Country>(request);
        if (country == null)
        {
            return UnexpectedPayload<Country>();
        }

        var document = CountryModel.MapFrom(country);
        await _countries.Update(document);
        return new OkObjectResult($"Updated {country}");
    }

    [Function("countries-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "countries")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        var countries = await _countries.ReadMany().Select(x => x.MapToDomain());
        return new OkObjectResult(countries);
    }
}
