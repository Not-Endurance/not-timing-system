using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class EnduranceEventFunctions : FunctionBase
{
    readonly IRepository<EnduranceEventModel> _events;

    public EnduranceEventFunctions(
        IFunctionLogger<EnduranceEventFunctions> logger,
        IRepository<EnduranceEventModel> events,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _events = events;
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

        await ReplaceCurrent(payload);
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

        await ReplaceCurrent(payload);
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

        var current = await GetCurrent(id);
        if (current == null)
        {
            return new NotFoundResult();
        }

        return Ok(current);
    }

    [Function("endurance-event-read-current")]
    public async Task<IActionResult> ReadCurrent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endurance-event")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadCurrent));
        TagRequest(request);
        LogInformation(request, nameof(ReadCurrent));

        var current = await GetCurrent(0);
        if (current == null)
        {
            return new NotFoundResult();
        }

        return Ok(current);
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

        if (id == 0)
        {
            var current = await _events.ReadMany();
            if (!current.Any())
            {
                return Ok();
            }

            foreach (var item in current)
            {
                await _events.Delete(item);
            }
            return Ok();
        }

        var document = await _events.Read(id);
        if (document == null)
        {
            return Ok();
        }

        await _events.Delete(document);
        return Ok();
    }

    async Task ReplaceCurrent(EnduranceEventModel payload)
    {
        var current = await _events.ReadMany();
        foreach (var item in current)
        {
            await _events.Delete(item);
        }

        await _events.Create(payload);
    }

    async Task<EnduranceEventModel?> GetCurrent(int id)
    {
        if (id != 0)
        {
            return await _events.Read(id);
        }

        return (await _events.ReadMany()).FirstOrDefault();
    }
}
