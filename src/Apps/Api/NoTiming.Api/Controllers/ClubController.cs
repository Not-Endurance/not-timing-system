using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NoTiming.Api.Base;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;

namespace NoTiming.Api.Controllers;

public class ClubController : CrudControllerBase<ClubModel>
{
    public ClubController(
        IFunctionLogger<ClubController> logger,
        IMongoRepository<ClubModel> clubs,
        ITelemetryService telemetry
    )
        : base(logger, clubs, telemetry) { }

    [HttpPost("clubs")]
    public async Task<IActionResult> Insert()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));
        return await CreateCore(request);
    }

    [HttpPatch("clubs")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [HttpDelete("clubs/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [HttpDelete("clubs")]
    public async Task<IActionResult> DeleteMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));
        return await DeleteManyCore(request);
    }

    [HttpGet("clubs/{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));
        return await ReadCore(id);
    }

    [HttpGet("clubs")]
    public async Task<IActionResult> List()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));
        return await ReadManyCore(request);
    }
}
