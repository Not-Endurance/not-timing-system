using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NoTiming.Api.Base;
using NoTiming.Api.Logging;
using NoTiming.Api.Telemetry;

namespace NoTiming.Api.Controllers;

public class SettingController : ApiControllerBase
{
    readonly IRepository<SettingModel> _settings;

    public SettingController(
        IFunctionLogger<SettingController> logger,
        IRepository<SettingModel> settings,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _settings = settings;
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Insert()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<SettingModel>(request);
        await _settings.Create(document);
        return Ok();
    }

    [HttpPatch("settings")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var document = await ReadBody<SettingModel>(request);
        await _settings.Update(document);
        return Ok();
    }

    [HttpGet("settings/{accountId}")]
    public async Task<IActionResult> GetOne(string accountId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        return Ok(await _settings.Read(x => x.AccountId == accountId));
    }
}
