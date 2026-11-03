using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.Warp.Api.Base;
using NTS.Nexus.Warp.Api.Logging;
using NTS.Nexus.Warp.Api.Telemetry;

namespace NTS.Nexus.Warp.Api.Controllers;

public class RankingController : CrudControllerBase<RankingModel>
{
    public RankingController(
        IFunctionLogger<RankingController> logger,
        IMongoRepository<RankingModel> rankings,
        ITelemetryService telemetry
    )
        : base(logger, rankings, telemetry) { }

    [HttpPost("api/rankings")]
    public async Task<IActionResult> Create()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await CreateCore(request);
    }

    [HttpPatch("api/rankings")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [HttpDelete("api/rankings/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [HttpDelete("api/rankings")]
    public async Task<IActionResult> DeleteMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));
        return await DeleteManyCore(request);
    }

    [HttpGet("api/rankings/{id:int}")]
    public async Task<IActionResult> Read(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await ReadCore(id);
    }

    [HttpGet("api/rankings")]
    public async Task<IActionResult> ReadMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await ReadManyCore(request);
    }
}
