using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class RankingFunctions : CrudFunctions<RankingModel>
{
    public RankingFunctions(
        IFunctionLogger<RankingFunctions> logger,
        IMongoRepository<RankingModel> rankings,
        ITelemetryService telemetry
    )
        : base(logger, rankings, telemetry) { }

    [Function("rankings-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rankings")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await CreateCore(request);
    }

    [Function("rankings-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "rankings")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [Function("rankings-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "rankings/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [Function("rankings-read")]
    public async Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rankings/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await ReadCore(id);
    }

    [Function("rankings-read-many")]
    public async Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rankings")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await ReadManyCore(request);
    }
}
