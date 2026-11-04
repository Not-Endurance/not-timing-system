using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NC.Storage.Mongo;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Api.Base;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;

namespace NoTiming.Api.Controllers;

public class SnapshotResultController : CrudControllerBase<SnapshotResultModel>
{
    public SnapshotResultController(
        IFunctionLogger<SnapshotResultController> logger,
        IMongoRepository<SnapshotResultModel> snapshotResults,
        ITelemetryService telemetry
    )
        : base(logger, snapshotResults, telemetry) { }

    [HttpPost("snapshot-results")]
    public async Task<IActionResult> Create()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await CreateCore(request);
    }

    [HttpPatch("snapshot-results")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [HttpDelete("snapshot-results/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [HttpDelete("snapshot-results")]
    public async Task<IActionResult> DeleteMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));
        return await DeleteManyCore(request);
    }

    [HttpGet("snapshot-results/{id:int}")]
    public async Task<IActionResult> Read(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await ReadCore(id);
    }

    [HttpGet("snapshot-results")]
    public async Task<IActionResult> ReadMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await ReadManyCore(request);
    }
}
