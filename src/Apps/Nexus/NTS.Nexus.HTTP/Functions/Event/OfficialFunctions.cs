using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Event;

public class OfficialFunctions : EventScopedCrudFunctions<OfficialModel>
{
    public OfficialFunctions(
        IFunctionLogger<OfficialFunctions> logger,
        IRepository<OfficialModel> officials,
        ITelemetryService telemetry
    )
        : base(logger, officials, telemetry) { }

    [Function("officials-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId:int}/officials")]
            HttpRequest request,
        int eventId
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await InternalCreate(request, eventId);
    }

    [Function("officials-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "events/{eventId:int}/officials")]
            HttpRequest request,
        int eventId
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await InternalUpdate(request, eventId);
    }

    [Function("officials-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId:int}/officials/{id:int}")]
            HttpRequest request,
        int eventId,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await InternalDelete(request, eventId, id);
    }

    [Function("officials-read")]
    public async Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId:int}/officials/{id:int}")]
            HttpRequest request,
        int eventId,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await InternalRead(request, eventId, id);
    }

    [Function("officials-read-many")]
    public async Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId:int}/officials")]
            HttpRequest request,
        int eventId
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await InternalReadMany(request, eventId);
    }
}
