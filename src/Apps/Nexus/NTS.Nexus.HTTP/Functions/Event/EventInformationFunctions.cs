using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Contracts.Core;
using NTS.Contracts.Core.Models;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Event;

public class EventInformationFunctions : FunctionBase
{
    readonly IRepository<EventInformationModel> _events;

    public EventInformationFunctions(
        IFunctionLogger<EventInformationFunctions> logger,
        IRepository<EventInformationModel> events,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _events = events;
    }

    [Function("event-information-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));

        var payload = await ReadBody<EventInformationModel>(request);
        await _events.Create(payload);
        return Ok();
    }

    [Function("event-information-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var payload = await ReadBody<EventInformationModel>(request);
        await _events.Update(payload);
        return Ok();
    }

    [Function("event-information-read")]
    public async Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "event/{id:guid}")] HttpRequest request,
        Guid id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));

        return Ok(await _events.Read(x => x.Id == id));
    }

    [Function("event-information-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _events.ReadMany() ?? []);
    }

    [Function("event-information-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "event/{id:guid}")] HttpRequest request,
        Guid id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var document = await _events.Read(x => x.Id == id);
        if (document == null)
        {
            return Ok();
        }

        await _events.DeleteMany(x => x.Id == id);
        return Ok();
    }
}
