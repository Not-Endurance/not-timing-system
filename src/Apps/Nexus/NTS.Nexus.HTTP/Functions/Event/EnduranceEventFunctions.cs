using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Structures;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Event;

public class EnduranceEventFunctions : FunctionBase
{
    readonly IRepository<EnduranceEventModel> _events;
    readonly IEnduranceEventResetService _resetService;
    readonly IEnduranceEventBusinessService _businessService;

    public EnduranceEventFunctions(
        IFunctionLogger<EnduranceEventFunctions> logger,
        IRepository<EnduranceEventModel> events,
        IEnduranceEventResetService resetService,
        IEnduranceEventBusinessService businessService,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _events = events;
        _resetService = resetService;
        _businessService = businessService;
    }

    [Function("endurance-event-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "endurance-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));

        var payload = await ReadBody<EnduranceEventModel>(request);
        if (payload == null)
        {
            return UnexpectedPayload<EnduranceEventModel>();
        }
        await _events.Create(payload);
        return Ok();
    }

    [Function("endurance-event-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "endurance-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var payload = await ReadBody<EnduranceEventModel>(request);
        if (payload == null)
        {
            return UnexpectedPayload<EnduranceEventModel>();
        }
        await _events.Update(payload);
        return Ok();
    }

    [Function("endurance-event-read")]
    public async Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endurance-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));

        var current = await _events.Read(x => x.Id == id);
        if (current == null)
        {
            return new NotFoundResult();
        }

        return Ok(current);
    }

    [Function("endurance-event-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endurance-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        var events = await _events.ReadMany();
        return Ok(events);
    }

    [Function("endurance-event-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "endurance-event/{id:int}")] HttpRequest request,
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

        await _events.Delete(x => x.Id == id);
        return Ok();
    }

    [Function("endurance-event-reset")]
    public async Task<IActionResult> Reset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "endurance-event/{id:int}/reset")]
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

    [Function("endurance-event-start")]
    public async Task<IActionResult> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "endurance-event/{id:int}/start")]
            HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Start));
        TagRequest(request);
        LogInformation(request, nameof(Start));

        try
        {
            var startedEvent = await _businessService.Start(id);
            return Ok(Result.Success(startedEvent));
        }
        catch (DomainException ex)
        {
            return Ok(Result.Failure<EnduranceEventModel>(ex.Message));
        }
    }
}
