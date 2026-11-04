using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NC.Application.CRUD.Ports;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Api.Base;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;

namespace NoTiming.Api.Controllers;

public class CountryController : ApiControllerBase
{
    readonly IRepository<CountryModel> _countries;

    public CountryController(
        IFunctionLogger<CountryController> logger,
        IRepository<CountryModel> countries,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _countries = countries;
    }

    [HttpPost("countries")]
    public async Task<IActionResult> Insert()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<CountryModel>(request);
        await _countries.Create(document);
        return Ok();
    }

    [HttpPatch("countries")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var document = await ReadBody<CountryModel>(request);
        await _countries.Update(document);
        return Ok();
    }

    [HttpGet("countries")]
    public async Task<IActionResult> List()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _countries.ReadMany() ?? []);
    }
}
