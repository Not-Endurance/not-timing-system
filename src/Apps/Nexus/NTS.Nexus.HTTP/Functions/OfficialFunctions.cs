using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class OfficialFunctions : CrudFunctions<OfficialModel>
{
    public OfficialFunctions(
        IFunctionLogger<OfficialFunctions> logger,
        IMongoRepository<OfficialModel> officials,
        ITelemetryService telemetry
    )
        : base(logger, officials, telemetry) { }

    [Function("officials-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "officials")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await CreateCore(request);
    }

    [Function("officials-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "officials")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [Function("officials-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "officials/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [Function("officials-delete-many")]
    public async Task<IActionResult> DeleteMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "officials")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));
        return await DeleteManyCore(request);
    }

    [Function("officials-read")]
    public async Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "officials/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await ReadCore(id);
    }

    [Function("officials-read-many")]
    public async Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "officials")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await ReadManyCore(request);
    }
}
