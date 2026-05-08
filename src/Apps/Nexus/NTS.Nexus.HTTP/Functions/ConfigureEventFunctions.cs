using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class ConfigureEventFunctions : FunctionBase
{
    readonly IRepository<ConfigureEventModel> _configureEvents;

    public ConfigureEventFunctions(
        IRepository<ConfigureEventModel> configureEventRepository,
        IFunctionLogger<ConfigureEventFunctions> logger,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _configureEvents = configureEventRepository;
    }

    [Function("configure-event-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "configure-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<ConfigureEventModel>(request);
        await _configureEvents.Create(document);
        return Ok();
    }

    [Function("configure-event-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "configure-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _configureEvents.ReadMany() ?? []);
    }

    [Function("configure-event-query-by-id")]
    public async Task<IActionResult> QueryById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "configure-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(QueryById));
        TagRequest(request);
        LogInformation(request, nameof(QueryById));

        return Ok(await _configureEvents.Read(id));
    }

    [Function("configure-event-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "configure-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var document = await ReadBody<ConfigureEventModel>(request);
        await _configureEvents.Update(document);
        return Ok();
    }

    [Function("configure-event-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "configure-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var configureEvent = await _configureEvents.Read(id);
        if (configureEvent == null)
        {
            return Ok();
        }

        await _configureEvents.Delete(configureEvent);
        return Ok();
    }

    [Function("configure-event-delete-many")]
    public async Task<IActionResult> DeleteMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "configure-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));

        var configureEvents = await ReadBody<ConfigureEventModel[]>(request);
        await _configureEvents.DeleteMany(configureEvents);
        return Ok();
    }
}
