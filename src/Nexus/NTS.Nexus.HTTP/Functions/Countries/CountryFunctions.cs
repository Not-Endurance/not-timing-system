using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Serialization;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Logger;
using NTS.Storage.Documents.Countries;

namespace NTS.Nexus.HTTP.Functions.Countries;

public class CountriesFunctions : FunctionBase<CountriesFunctions>
{
    readonly IRepository<CountryDocument> _countries;

    public CountriesFunctions(
        IFunctionLogger<CountriesFunctions> logger,
        IRepository<CountryDocument> countries
    )
        : base(logger)
    {
        _countries = countries;
    }

    [Function("country-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "countries")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var country = requestBody.FromConvertedJson<Country>();
        var document = new CountryDocument(country);
        await _countries.Create(document);

        return new OkObjectResult($"Inserted {country}");
    }

    [Function("country-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "countries")]
            HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var country = requestBody.FromConvertedJson<Country>();
        var document = new CountryDocument(country);
        await _countries.Update(document);

        return new OkObjectResult($"Updated {country}");
    }

    [Function("country-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "countries")] HttpRequest request
    )
    {
        LogInformation(request);

        var countries = await _countries.ReadAll();
        return new OkObjectResult(countries);
    }
}
