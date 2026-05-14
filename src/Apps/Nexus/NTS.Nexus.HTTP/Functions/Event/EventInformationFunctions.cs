using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Event;

public class EventInformationFunctions : FunctionBase
{
    readonly IRepository<EventInformationModel> _events;
    readonly IEventInformationResetService _resetService;
    readonly IEventInformationBusinessService _businessService;

    public EventInformationFunctions(
        IFunctionLogger<EventInformationFunctions> logger,
        IRepository<EventInformationModel> events,
        IEventInformationResetService resetService,
        IEventInformationBusinessService businessService,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _events = events;
        _resetService = resetService;
        _businessService = businessService;
    }

    [Function("event-information-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "event-information")] HttpRequest request
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "event-information")] HttpRequest request
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "event-information/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));

        return Ok(await _events.Read(x => x.Id == id));
    }

    [Function("event-information-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "event-information")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _events.ReadMany() ?? []);
    }

    [Function("event-information-active-list")]
    public async Task<IActionResult> ListActive(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "event-information/active")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ListActive));
        TagRequest(request);
        LogInformation(request, nameof(ListActive));

        return Ok(await _businessService.ReadActive());
    }

    [Function("event-information-past-list")]
    public async Task<IActionResult> ListPast(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "event-information/past")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ListPast));
        TagRequest(request);
        LogInformation(request, nameof(ListPast));

        var events = await _businessService.ReadPast();
        return Ok(events);
    }

    [Function("event-information-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "event-information/{id:int}")] HttpRequest request,
        int id
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

    [Function("event-information-reset")]
    public async Task<IActionResult> Reset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "event-information/{id:int}/reset")]
            HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Reset));
        TagRequest(request);
        LogInformation(request, nameof(Reset));

        await _resetService.Reset(id);
        return Ok();
    }

    [Function("event-information-deactivate")]
    public async Task<IActionResult> Deactivate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "event-information/{id:int}/deactivate")]
            HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Deactivate));
        TagRequest(request);
        LogInformation(request, nameof(Deactivate));

        await _businessService.Deactivate(id);
        return Ok();
    }

    [Function("event-information-start")]
    public async Task<IActionResult> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "event-information/{id:int}/start")]
            HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Start));
        TagRequest(request);
        LogInformation(request, nameof(Start));

        return Ok(await _businessService.Start(id));
    }
}
