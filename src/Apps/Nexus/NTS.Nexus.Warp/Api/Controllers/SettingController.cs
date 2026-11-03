using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Nexus.Warp.Api.Base;
using NTS.Nexus.Warp.Api.Logging;
using NTS.Nexus.Warp.Api.Telemetry;

namespace NTS.Nexus.Warp.Api.Controllers;

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

    [HttpPost("api/settings")]
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

    [HttpPatch("api/settings")]
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

    [HttpGet("api/settings/{accountId}")]
    public async Task<IActionResult> GetOne(string accountId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        return Ok(await _settings.Read(x => x.AccountId == accountId));
    }
}
