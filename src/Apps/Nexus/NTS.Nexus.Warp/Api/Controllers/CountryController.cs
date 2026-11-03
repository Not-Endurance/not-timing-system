using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Nexus.Warp.Api.Base;
using NTS.Nexus.Warp.Api.Logging;
using NTS.Nexus.Warp.Api.Telemetry;

namespace NTS.Nexus.Warp.Api.Controllers;

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

    [HttpPost("api/countries")]
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

    [HttpPatch("api/countries")]
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

    [HttpGet("api/countries")]
    public async Task<IActionResult> List()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _countries.ReadMany() ?? []);
    }
}
