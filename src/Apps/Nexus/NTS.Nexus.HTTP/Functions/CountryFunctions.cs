using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Async.Extensions;
using Not.Serialization.JSON;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions;

public class CountryFunctions : FunctionBase
{
    readonly IRepository<CountryModel> _countries;

    public CountryFunctions(IFunctionLogger<CountryFunctions> logger, IRepository<CountryModel> countries)
        : base(logger)
    {
        _countries = countries;
    }

    [Function("countries-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "countries")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var country = requestBody.FromJson<Country>();
        var document = CountryModel.From(country);
        await _countries.Create(document);

        return new OkObjectResult($"Inserted {country}");
    }

    [Function("countries-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "countries")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var country = requestBody.FromJson<Country>();
        var document = CountryModel.From(country);
        await _countries.Update(document);

        return new OkObjectResult($"Updated {country}");
    }

    [Function("countries-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "countries")] HttpRequest request
    )
    {
        LogInformation(request);

        var countries = await _countries.ReadMany().Select(x => x.MapToEntity());
        return new OkObjectResult(countries);
    }
}
