using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Nexus.Warp.Api.Base;
using NTS.Nexus.Warp.Api.Logging;
using NTS.Nexus.Warp.Api.Telemetry;

namespace NTS.Nexus.Warp.Api.Controllers;

public class HorseController : ApiControllerBase
{
    readonly IRepository<HorseModel> _horses;

    public HorseController(
        IFunctionLogger<HorseController> logger,
        IRepository<HorseModel> horses,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _horses = horses;
    }

    [HttpPost("horses")]
    public async Task<IActionResult> Insert()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<HorseModel>(request);
        await _horses.Create(document);
        return Ok();
    }

    [HttpPatch("horses")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var document = await ReadBody<HorseModel>(request);
        await _horses.Update(document);
        return Ok();
    }

    [HttpDelete("horses/{id:int}/safe")]
    public async Task<IActionResult> SafeDelete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(SafeDelete));
        TagRequest(request);
        LogInformation(request, nameof(SafeDelete));

        var horse = await _horses.Read(id);
        if (horse == null)
        {
            return Ok();
        }

        await _horses.Delete(horse);
        return Ok();
    }

    [HttpDelete("horses/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var horse = await _horses.Read(id);
        if (horse == null)
        {
            return Ok();
        }

        await _horses.Delete(horse);
        return Ok();
    }

    [HttpDelete("horses")]
    public async Task<IActionResult> DeleteMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));

        var horses = await ReadBody<HorseModel[]>(request);
        await _horses.DeleteMany(horses);
        return Ok();
    }

    [HttpGet("horses/{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        return Ok(await _horses.Read(id));
    }

    [HttpGet("horses")]
    public async Task<IActionResult> List()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _horses.ReadMany() ?? []);
    }
}
